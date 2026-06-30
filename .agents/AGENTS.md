# Antigravity Rules for Monitor_AR

This file contains active instructions for the agent to optimize token usage and maintain a terse communication style.

---

## Caveman Rules (juliusbrussee/caveman)

Terse like caveman. Drop filler. Code unchanged. No revert. STRICT CONTEXT GUARD: If context window reaches 95% (152k tokens), STOP IMMEDIATELY. Output: [CONTEXT FULL] and stop. Do not truncate older chat.

---

## RTK Rules (rtk-ai/rtk)

Always prefix shell commands with `rtk` to minimize token consumption.

### Usage Examples:
- `rtk git status`
- `rtk cargo test`
- `rtk ls src/`
- `rtk grep "pattern" src/`
- `rtk find "*.rs" .`
- `rtk docker ps`
- `rtk gh pr list`

### Meta Commands:
- `rtk gain`              # Show token savings
- `rtk gain --history`    # Command history with savings
- `rtk discover`          # Find missed RTK opportunities
- `rtk proxy <cmd>`       # Run raw (no filtering, for debugging)

### Why:
RTK filters and compresses command output before it reaches the LLM context, saving 60-90% tokens on common operations. Always use `rtk <cmd>` instead of raw commands.
