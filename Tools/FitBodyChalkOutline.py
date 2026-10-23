"""Fit a floor outline to an exported world-space body mesh, preserving concavities.

Input/output: docs/evidence/body-chalk-fit. Uses the current mesh pose, not a
convex hull or a generic human outline. No Unity runtime dependencies.
"""
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = Path(__file__).resolve().parents[1] / "docs/evidence/body-chalk-fit"
data = json.loads((OUT / "body-mesh.json").read_text())
vertices = np.array(data["vertices"])
triangles = np.array(data["triangles"]).reshape(-1, 3)
step = .0015
low = vertices[:, [0, 2]].min(axis=0) - .08
high = vertices[:, [0, 2]].max(axis=0) + .08
width, height = np.ceil((high-low)/step).astype(int)+1
pixels = np.rint(np.column_stack(((vertices[:, 0]-low[0])/step,
                                 (high[1]-vertices[:, 2])/step))).astype(int)
silhouette = Image.new("L", (int(width), int(height)), 0)
draw = ImageDraw.Draw(silhouette)
for triangle in triangles:
    draw.polygon([tuple(pixels[i]) for i in triangle], fill=255)

# 21 mm axis clearance leaves the original 22 mm stroke outside the body.
mask = np.array(silhouette.filter(ImageFilter.MaxFilter(29))) > 0
edges = {}


def add(x0, y0, x1, y1):
    edges.setdefault((int(x0), int(y0)), []).append((int(x1), int(y1)))


up = np.pad(mask[:-1], ((1, 0), (0, 0)))
down = np.pad(mask[1:], ((0, 1), (0, 0)))
left = np.pad(mask[:, :-1], ((0, 0), (1, 0)))
right = np.pad(mask[:, 1:], ((0, 0), (0, 1)))
for y, x in zip(*np.nonzero(mask & ~up)):
    add(x, y, x+1, y)
for y, x in zip(*np.nonzero(mask & ~right)):
    add(x+1, y, x+1, y+1)
for y, x in zip(*np.nonzero(mask & ~down)):
    add(x+1, y+1, x, y+1)
for y, x in zip(*np.nonzero(mask & ~left)):
    add(x, y+1, x, y)


def cross(a, b):
    return a[..., 0]*b[..., 1]-a[..., 1]*b[..., 0]


loops = []
while edges:
    start = next(iter(edges))
    current, previous = start, (start[0]-1, start[1])
    loop = [current]
    while True:
        choices = edges[current]
        incoming = np.array(current)-previous

        def turn(point):
            outgoing = np.array(point)-current
            return np.arctan2(cross(incoming, outgoing), incoming @ outgoing)

        following = max(choices, key=turn)
        choices.remove(following)
        if not choices:
            del edges[current]
        previous, current = current, following
        if current == start:
            break
        loop.append(current)
    loops.append(np.array(loop, dtype=float))

loop = max(loops, key=lambda q: abs(cross(q, np.roll(q, -1, axis=0)).sum()))
world = np.column_stack((low[0]+loop[:, 0]*step, high[1]-loop[:, 1]*step))


def simplify(points, tolerance):
    if len(points) < 3:
        return points
    delta = points[-1]-points[0]
    u = np.clip((points-points[0]) @ delta/max(delta @ delta, 1e-12), 0, 1)
    distances = np.linalg.norm(points-(points[0]+u[:, None]*delta), axis=1)
    index = np.argmax(distances)
    if distances[index] <= tolerance:
        return points[[0, -1]]
    return np.concatenate((simplify(points[:index+1], tolerance)[:-1],
                           simplify(points[index:], tolerance)))


opposite = np.argmax(np.linalg.norm(world-world[0], axis=1))
simple = np.concatenate((simplify(world[:opposite+1], .0045)[:-1],
                         simplify(np.concatenate((world[opposite:], world[:1])), .0045)[:-1]))
points = []
for a, b in zip(simple, np.roll(simple, -1, axis=0)):
    count = max(1, int(np.ceil(np.linalg.norm(b-a)/.11)))
    points.extend(a+(b-a)*i/count for i in range(count))
points = np.array(points)
start = np.argmin(np.linalg.norm(points-np.array([17.95032, 12.57654]), axis=1))
points = np.roll(points, -start, axis=0)
result = {"points": [[float(x), 1.218, float(z)] for x, z in points],
          "marginMetres": .021, "rasterMetresPerPixel": step,
          "simplificationMetres": .0045, "count": len(points)}
(OUT / "fitted-points.json").write_text(json.dumps(result, indent=2)+"\n")

preview = Image.new("RGB", silhouette.size, (35, 40, 45))
draw = ImageDraw.Draw(preview)
for triangle in triangles:
    draw.polygon([tuple(pixels[i]) for i in triangle], fill=(135, 145, 155))
outline_pixels = [((x-low[0])/step, (high[1]-z)/step) for x, _, z in result["points"]]
draw.line(outline_pixels+[outline_pixels[0]], fill=(255, 225, 120), width=7)
for x, y in outline_pixels:
    draw.ellipse((x-2, y-2, x+2, y+2), fill=(255, 255, 255))
preview.save(OUT / "contour-design.png")
print(json.dumps({"count": len(points), "silhouetteLoops": len(loops),
                  "outlineLength": float(np.linalg.norm(points-np.roll(points, -1, axis=0), axis=1).sum())}))
