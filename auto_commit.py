#!/usr/bin/env python3
"""
╔══════════════════════════════════════════════════════════════════════════╗
║  auto_commit.py — Script Tự Động Commit khi Phát Hiện Thay Đổi        ║
║                                                                        ║
║  Dự án  : Motion AR — Monitor AR Technical Training Simulator          ║
║  Tác giả: Auto-generated for Motion_AR project                        ║
║                                                                        ║
║  Chức năng:                                                            ║
║    • Theo dõi toàn bộ thư mục dự án (trừ paths trong .gitignore)      ║
║    • Khi phát hiện thay đổi → tự động git add + commit                ║
║    • Cơ chế debounce: gom thay đổi trong 5 giây thành 1 commit       ║
║    • Tùy chọn auto-push lên remote (mặc định: tắt)                   ║
║                                                                        ║
║  Cách chạy:                                                            ║
║    pip install watchdog                                                ║
║    python auto_commit.py                                               ║
║    python auto_commit.py --push           # Bật auto-push             ║
║    python auto_commit.py --debounce 10    # Debounce 10 giây          ║
║    python auto_commit.py --path /my/repo  # Chỉ định thư mục khác    ║
║                                                                        ║
║  Dừng script: Nhấn Ctrl+C                                             ║
╚══════════════════════════════════════════════════════════════════════════╝
"""

import os
import sys
import time
import subprocess
import argparse
import threading
import logging
import signal
import fnmatch
from pathlib import Path
from datetime import datetime

# Đảm bảo console Windows hiển thị Unicode (emoji, tiếng Việt) đúng cách
if sys.platform == "win32":
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except (AttributeError, OSError):
        pass


try:
    from watchdog.observers import Observer
    from watchdog.events import FileSystemEventHandler
except ImportError:
    print("=" * 60)
    print("  LỖI: Thư viện 'watchdog' chưa được cài đặt.")
    print("  Chạy lệnh sau để cài đặt:")
    print()
    print("    pip install watchdog")
    print()
    print("=" * 60)
    sys.exit(1)


# ═══════════════════════════════════════════════════════════════════════════════
# CẤU HÌNH MẶC ĐỊNH
# ═══════════════════════════════════════════════════════════════════════════════

DEFAULT_DEBOUNCE_SECONDS = 5
DEFAULT_REPO_PATH = os.path.dirname(os.path.abspath(__file__))
LOG_FILE = os.path.join(DEFAULT_REPO_PATH, "auto_commit.log")

# Các pattern luôn bị bỏ qua (ngoài .gitignore)
ALWAYS_IGNORE_PATTERNS = [
    ".git",
    ".git/*",
    "auto_commit.log",
    "*.pyc",
    "__pycache__",
    ".venv",
    ".venv/*",
]


# ═══════════════════════════════════════════════════════════════════════════════
# TIỆN ÍCH
# ═══════════════════════════════════════════════════════════════════════════════


def setup_logging(log_file: str) -> logging.Logger:
    """Thiết lập logging ra console và file."""
    logger = logging.getLogger("auto_commit")
    logger.setLevel(logging.INFO)

    # Console handler (hiển thị trên terminal)
    console_handler = logging.StreamHandler(sys.stdout)
    console_handler.setLevel(logging.INFO)
    console_fmt = logging.Formatter(
        "%(asctime)s │ %(levelname)-7s │ %(message)s",
        datefmt="%H:%M:%S",
    )
    console_handler.setFormatter(console_fmt)
    logger.addHandler(console_handler)

    # File handler (ghi vào file log)
    try:
        file_handler = logging.FileHandler(log_file, encoding="utf-8")
        file_handler.setLevel(logging.DEBUG)
        file_fmt = logging.Formatter(
            "%(asctime)s │ %(levelname)-7s │ %(message)s",
            datefmt="%Y-%m-%d %H:%M:%S",
        )
        file_handler.setFormatter(file_fmt)
        logger.addHandler(file_handler)
    except OSError:
        logger.warning(f"Không thể tạo file log tại: {log_file}")

    return logger


def run_git_command(args: list, cwd: str, logger: logging.Logger) -> tuple:
    """
    Chạy lệnh git và trả về (return_code, stdout, stderr).
    """
    try:
        result = subprocess.run(
            ["git"] + args,
            cwd=cwd,
            capture_output=True,
            text=True,
            timeout=30,
        )
        return result.returncode, result.stdout.strip(), result.stderr.strip()
    except subprocess.TimeoutExpired:
        logger.error(f"Git command timed out: git {' '.join(args)}")
        return -1, "", "Timeout"
    except FileNotFoundError:
        logger.error("Git không được cài đặt hoặc không có trong PATH.")
        return -1, "", "Git not found"


def parse_gitignore(repo_path: str) -> list:
    """
    Đọc file .gitignore và trả về danh sách các pattern.
    Bỏ qua comment (#) và dòng trống.
    """
    gitignore_path = os.path.join(repo_path, ".gitignore")
    patterns = []

    if not os.path.exists(gitignore_path):
        return patterns

    try:
        with open(gitignore_path, "r", encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                # Bỏ qua comment và dòng trống
                if not line or line.startswith("#"):
                    continue
                # Bỏ qua dòng ngoại lệ (bắt đầu bằng !)
                if line.startswith("!"):
                    continue
                patterns.append(line)
    except OSError:
        pass

    return patterns


def should_ignore(file_path: str, repo_path: str, ignore_patterns: list) -> bool:
    """
    Kiểm tra xem file có nên bị bỏ qua không.
    Dựa trên .gitignore patterns và ALWAYS_IGNORE_PATTERNS.
    """
    # Lấy đường dẫn tương đối so với repo
    try:
        rel_path = os.path.relpath(file_path, repo_path)
    except ValueError:
        return True

    # Chuẩn hóa separator
    rel_path = rel_path.replace("\\", "/")

    # Kiểm tra ALWAYS_IGNORE_PATTERNS
    for pattern in ALWAYS_IGNORE_PATTERNS:
        pattern_clean = pattern.rstrip("/")
        # Kiểm tra từng phần của đường dẫn
        parts = rel_path.split("/")
        for part in parts:
            if fnmatch.fnmatch(part, pattern_clean):
                return True
        # Kiểm tra toàn bộ đường dẫn
        if fnmatch.fnmatch(rel_path, pattern):
            return True

    # Kiểm tra .gitignore patterns
    for pattern in ignore_patterns:
        pattern_clean = pattern.rstrip("/")

        # Pattern dạng thư mục (kết thúc bằng /)
        if pattern.endswith("/"):
            parts = rel_path.split("/")
            for part in parts:
                if fnmatch.fnmatch(part, pattern_clean):
                    return True

        # Pattern dạng file hoặc wildcard
        parts = rel_path.split("/")
        for part in parts:
            if fnmatch.fnmatch(part, pattern_clean):
                return True

        # Kiểm tra đường dẫn đầy đủ
        if fnmatch.fnmatch(rel_path, pattern):
            return True
        if fnmatch.fnmatch(rel_path, pattern_clean):
            return True

    return False


def get_changed_files_summary(cwd: str, logger: logging.Logger) -> str:
    """
    Lấy danh sách file thay đổi từ git status và tạo mô tả ngắn gọn.
    """
    code, stdout, _ = run_git_command(
        ["status", "--porcelain", "--short"], cwd, logger
    )
    if code != 0 or not stdout:
        return "cập nhật dự án"

    lines = stdout.strip().split("\n")
    added = []
    modified = []
    deleted = []

    for line in lines:
        if len(line) < 3:
            continue
        status = line[:2].strip()
        filename = line[3:].strip().strip('"')
        # Chỉ lấy tên file (không lấy đường dẫn dài)
        basename = os.path.basename(filename)

        if status in ("A", "?"):
            added.append(basename)
        elif status == "M":
            modified.append(basename)
        elif status == "D":
            deleted.append(basename)

    # Tạo mô tả commit message
    parts = []
    if added:
        if len(added) <= 3:
            parts.append(f"thêm {', '.join(added)}")
        else:
            parts.append(f"thêm {len(added)} files")

    if modified:
        if len(modified) <= 3:
            parts.append(f"sửa {', '.join(modified)}")
        else:
            parts.append(f"sửa {len(modified)} files")

    if deleted:
        if len(deleted) <= 3:
            parts.append(f"xóa {', '.join(deleted)}")
        else:
            parts.append(f"xóa {len(deleted)} files")

    if not parts:
        return "cập nhật dự án"

    return "; ".join(parts)


# ═══════════════════════════════════════════════════════════════════════════════
# WATCHER — Theo dõi file system
# ═══════════════════════════════════════════════════════════════════════════════


class ChangeHandler(FileSystemEventHandler):
    """
    Handler cho watchdog — phát hiện thay đổi file và kích hoạt debounce timer.
    """

    def __init__(
        self,
        repo_path: str,
        ignore_patterns: list,
        debounce_seconds: float,
        auto_push: bool,
        logger: logging.Logger,
    ):
        super().__init__()
        self.repo_path = repo_path
        self.ignore_patterns = ignore_patterns
        self.debounce_seconds = debounce_seconds
        self.auto_push = auto_push
        self.logger = logger

        self._timer = None
        self._lock = threading.Lock()
        self._pending_changes = set()
        self._commit_count = 0

    def _on_any_change(self, event):
        """Xử lý mọi loại thay đổi (tạo, sửa, xóa, di chuyển)."""
        # Bỏ qua thay đổi thư mục (chỉ quan tâm file)
        if event.is_directory:
            return

        file_path = event.src_path

        # Kiểm tra xem file có nên bị bỏ qua không
        if should_ignore(file_path, self.repo_path, self.ignore_patterns):
            return

        # Ghi nhận thay đổi
        rel_path = os.path.relpath(file_path, self.repo_path).replace("\\", "/")
        with self._lock:
            self._pending_changes.add(rel_path)

        self.logger.debug(f"Phát hiện: {event.event_type} → {rel_path}")

        # Reset debounce timer
        self._reset_timer()

    def on_created(self, event):
        self._on_any_change(event)

    def on_modified(self, event):
        self._on_any_change(event)

    def on_deleted(self, event):
        self._on_any_change(event)

    def on_moved(self, event):
        self._on_any_change(event)

    def _reset_timer(self):
        """Reset debounce timer — mỗi thay đổi mới sẽ kéo dài thời gian chờ."""
        with self._lock:
            if self._timer is not None:
                self._timer.cancel()
            self._timer = threading.Timer(
                self.debounce_seconds, self._do_commit
            )
            self._timer.daemon = True
            self._timer.start()

    def _do_commit(self):
        """Thực hiện git add + commit sau khi debounce timer hết hạn."""
        with self._lock:
            changes = self._pending_changes.copy()
            self._pending_changes.clear()

        if not changes:
            return

        self.logger.info(f"⏳ Debounce hết hạn — {len(changes)} file(s) thay đổi")

        # 1. Git add -A (thêm tất cả thay đổi, .gitignore sẽ tự lọc)
        code, _, stderr = run_git_command(["add", "-A"], self.repo_path, self.logger)
        if code != 0:
            self.logger.error(f"❌ git add thất bại: {stderr}")
            return

        # 2. Kiểm tra xem có gì để commit không
        code, status_output, _ = run_git_command(
            ["status", "--porcelain"], self.repo_path, self.logger
        )
        if code != 0 or not status_output.strip():
            self.logger.info("ℹ️  Không có thay đổi nào để commit (đã bị .gitignore lọc)")
            return

        # 3. Tạo commit message mô tả
        summary = get_changed_files_summary(self.repo_path, self.logger)
        timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        commit_msg = f"auto: {summary} [{timestamp}]"

        # 4. Git commit
        code, stdout, stderr = run_git_command(
            ["commit", "-m", commit_msg], self.repo_path, self.logger
        )
        if code != 0:
            if "nothing to commit" in stderr or "nothing to commit" in stdout:
                self.logger.info("ℹ️  Không có thay đổi mới để commit")
            else:
                self.logger.error(f"❌ git commit thất bại: {stderr}")
            return

        self._commit_count += 1
        self.logger.info(f"✅ Commit #{self._commit_count}: {commit_msg}")

        # 5. Auto-push (nếu được bật)
        if self.auto_push:
            self._do_push()

    def _do_push(self):
        """Push lên remote repository."""
        self.logger.info("📤 Đang push lên remote...")
        code, stdout, stderr = run_git_command(
            ["push"], self.repo_path, self.logger
        )
        if code == 0:
            self.logger.info("✅ Push thành công!")
        else:
            self.logger.error(f"❌ Push thất bại: {stderr}")
            self.logger.info(
                "💡 Gợi ý: Kiểm tra remote đã được cấu hình chưa "
                "(git remote add origin <url>)"
            )


# ═══════════════════════════════════════════════════════════════════════════════
# MAIN — Điểm khởi chạy chính
# ═══════════════════════════════════════════════════════════════════════════════


def main():
    parser = argparse.ArgumentParser(
        description="Auto-commit script -- Tu dong commit khi co thay doi trong du an",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Vi du su dung:
  python auto_commit.py                    # Mac dinh: chi commit local
  python auto_commit.py --push             # Bat auto-push len remote
  python auto_commit.py --debounce 10      # Cho 10 giay truoc khi commit
  python auto_commit.py --path D:\\MyRepo   # Theo doi thu muc khac

Dừng script: Nhấn Ctrl+C
        """,
    )

    parser.add_argument(
        "--path",
        type=str,
        default=DEFAULT_REPO_PATH,
        help=f"Đường dẫn thư mục dự án (mặc định: {DEFAULT_REPO_PATH})",
    )
    parser.add_argument(
        "--debounce",
        type=float,
        default=DEFAULT_DEBOUNCE_SECONDS,
        help=f"Thời gian debounce (giây) — gom thay đổi trước khi commit "
             f"(mặc định: {DEFAULT_DEBOUNCE_SECONDS}s)",
    )
    parser.add_argument(
        "--push",
        action="store_true",
        default=False,
        help="Tự động push lên remote sau mỗi commit (mặc định: tắt)",
    )
    parser.add_argument(
        "--log-file",
        type=str,
        default=LOG_FILE,
        help=f"Đường dẫn file log (mặc định: {LOG_FILE})",
    )

    # Hỗ trợ biến môi trường
    args = parser.parse_args()

    # Kiểm tra biến môi trường AUTO_PUSH
    if os.environ.get("AUTO_PUSH", "").lower() in ("1", "true", "yes"):
        args.push = True

    # Kiểm tra biến môi trường DEBOUNCE_SECONDS
    env_debounce = os.environ.get("DEBOUNCE_SECONDS")
    if env_debounce:
        try:
            args.debounce = float(env_debounce)
        except ValueError:
            pass

    # ── Setup ──────────────────────────────────────────────────────────
    logger = setup_logging(args.log_file)
    repo_path = os.path.abspath(args.path)

    # Verify thư mục là git repository
    if not os.path.isdir(os.path.join(repo_path, ".git")):
        logger.error(f"❌ Thư mục không phải là git repository: {repo_path}")
        logger.info("💡 Chạy 'git init' trước khi sử dụng script này.")
        sys.exit(1)

    # Đọc .gitignore
    ignore_patterns = parse_gitignore(repo_path)

    # ── Banner ─────────────────────────────────────────────────────────
    logger.info("=" * 60)
    logger.info("  🔄 AUTO-COMMIT WATCHER — Motion AR Project")
    logger.info("=" * 60)
    logger.info(f"  📁 Thư mục theo dõi : {repo_path}")
    logger.info(f"  ⏱️  Debounce         : {args.debounce} giây")
    logger.info(f"  📤 Auto-push        : {'BẬT ✅' if args.push else 'TẮT ❌'}")
    logger.info(f"  📝 Log file         : {args.log_file}")
    logger.info(f"  🚫 Gitignore rules  : {len(ignore_patterns)} patterns")
    logger.info("=" * 60)
    logger.info("  Nhấn Ctrl+C để dừng script")
    logger.info("=" * 60)
    logger.info("")

    # ── Khởi tạo Observer ──────────────────────────────────────────────
    handler = ChangeHandler(
        repo_path=repo_path,
        ignore_patterns=ignore_patterns,
        debounce_seconds=args.debounce,
        auto_push=args.push,
        logger=logger,
    )

    observer = Observer()
    observer.schedule(handler, repo_path, recursive=True)

    # ── Xử lý tín hiệu dừng ──────────────────────────────────────────
    stop_event = threading.Event()

    def signal_handler(signum, frame):
        logger.info("")
        logger.info("🛑 Nhận tín hiệu dừng — đang tắt watcher...")
        stop_event.set()

    signal.signal(signal.SIGINT, signal_handler)
    signal.signal(signal.SIGTERM, signal_handler)

    # ── Chạy ───────────────────────────────────────────────────────────
    observer.start()
    logger.info("👁️  Đang theo dõi thay đổi...")

    try:
        while not stop_event.is_set():
            stop_event.wait(timeout=1.0)
    except KeyboardInterrupt:
        pass
    finally:
        observer.stop()
        observer.join()

        # Commit pending changes nếu có
        if handler._pending_changes:
            logger.info("⏳ Commit các thay đổi còn pending trước khi thoát...")
            handler._do_commit()

        logger.info(f"📊 Tổng số commit đã tạo: {handler._commit_count}")
        logger.info("👋 Auto-commit watcher đã dừng. Tạm biệt!")


if __name__ == "__main__":
    main()
