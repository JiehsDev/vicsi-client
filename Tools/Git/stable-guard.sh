#!/bin/sh
# ViCSI stable checkpoint guard. Preserve Git LFS hooks separately.
branch=$(git symbolic-ref --quiet --short HEAD || true)
case "$branch" in
  stable/*)
    if [ "${VICSI_APPROVED_PROMOTION:-}" != "1" ]; then
      printf '%s\n' "ViCSI: stable branches are frozen. Work on develop/research or develop/competitive." "Promote only after the user explicitly accepts the next version; see docs/CHECKPOINT_WORKFLOW.md." >&2
      exit 1
    fi
    ;;
esac
exit 0
