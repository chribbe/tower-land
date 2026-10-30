#!/bin/sh
# Render the Game scene's main camera at the mockup resolution and compare it with the mockup.
# usage: Tools/VisualCompare/shot.sh <out_dir> <name> [seed]     (Unity editor must be open with the Pipeline package)
OUT=$1; NAME=$2; SEED=${3:-4}
MOCKUP="D:/ASG/TowerLand/Assets/TowerLand/Art/Reference/Mockup_Gameplay.png"
DIR=$(dirname "$0")
mkdir -p "$OUT"
unity --no-banner --no-pager cmd script eval --code "UnityEngine.Object.FindFirstObjectByType<TowerLand.Gameplay.LevelBootstrap>().Generate($SEED); TowerLand.Editor.CoreAssetsBuilder.RenderMainCamera(@\"$OUT/$NAME.png\", 941, 1672); return \"ok\";" --timeout 120 --result-only >/dev/null
python -I "$DIR/compare.py" "$OUT/$NAME.png" "$MOCKUP" "$OUT/${NAME}_cmp.png"     # ours | mockup | 50% blend
python -I "$DIR/crops.py" "$OUT/$NAME.png" "$MOCKUP" "$OUT/${NAME}_crops.png"     # region close-ups
echo "$OUT/${NAME}_cmp.png"
