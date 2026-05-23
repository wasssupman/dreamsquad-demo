# Reference Algorithm (TypeScript)

**용도**: Unit 3 (`3_incremental_path_builder.md`) 의 C#/Unity 포팅 reference. 컴파일 대상이 아니다. `.md` 확장자로 Unity asmdef 가 무시하도록 보관.

**포팅 시 주의**:
- `Math.random()` 은 `Unity.Mathematics.Random` (seed 결정성) 으로 교체.
- 모든 `Point/Goal` 은 `int2` 로.
- `Set<string> key()` 패턴은 `NativeHashSet<int2>` 또는 `int2 → cellIndex` 변환 후 `NativeHashSet<int>` 로.
- 인접 셀 4방향 enumeration 은 `Battle.Movement` 의 기존 helper 와 일치시킬 것.
- `maxMapAttempts` / `maxRouteAttempts` 는 `GenerationSettings` SO 노출.
- `goalMode = "edge" | "mixed"` 경로는 **본 spec 범위 밖**. `center` 만 포팅하고 edge/mixed 코드는 옮기지 않는다.
- `pickSpawns` 의 "edge cell 어디든" 를 **4분면 corner zone 으로 한정** 하도록 교체. distance 룰(spawn↔goal ≥ 7, spawn↔spawn ≥ 3) 은 유지하되, `min(W,H)/2` 와 `8` 의 max 로 spawn↔goal 하한 갱신.
- `generateCandidateRoutes` 의 28회 랜덤 미드포인트는 Burst 친화적으로 루프 풀거나 별도 helper 로 추출.
- 최종 validator 에 **지류당 꺾임 ≥ 3 / 셀 수 ≥ minBranchLen** 검사 추가.

## Reference Implementation

```typescript
type GoalMode = "edge" | "center" | "mixed";

type Point = {
  x: number;
  y: number;
};

type Goal = Point & {
  type: "left" | "right" | "top" | "bottom" | "center";
};

type GeneratedMap = {
  width: number;
  height: number;
  goal: Goal;
  spawns: Point[];
  path: Point[];
  merges: Point[];
  attempts: number;
};

type GenerateOptions = {
  width?: number;
  height?: number;
  spawnCount?: 1 | 2 | 3 | 4;
  goalMode?: GoalMode;
  maxMapAttempts?: number;
  maxRouteAttempts?: number;
};

export function generateDefenseMap(options: GenerateOptions = {}): GeneratedMap {
  const width = options.width ?? 20;
  const height = options.height ?? 10;
  const spawnCount = options.spawnCount ?? 4;
  const goalMode = options.goalMode ?? "mixed";
  const maxMapAttempts = options.maxMapAttempts ?? 600;
  const maxRouteAttempts = options.maxRouteAttempts ?? 160;

  const CENTER_MIN_X = Math.floor(width / 2) - 1;
  const CENTER_MAX_X = CENTER_MIN_X + 2;
  const CENTER_MIN_Y = Math.floor(height / 2) - 1;
  const CENTER_MAX_Y = CENTER_MIN_Y + 2;

  const key = (p: Point) => `${p.x},${p.y}`;

  const fromKey = (k: string): Point => {
    const [x, y] = k.split(",").map(Number);
    return { x, y };
  };

  const randInt = (min: number, max: number) =>
    Math.floor(Math.random() * (max - min + 1)) + min;

  const pick = <T,>(arr: T[]) => arr[randInt(0, arr.length - 1)];

  const shuffle = <T,>(arr: T[]) => {
    const copy = [...arr];
    for (let i = copy.length - 1; i > 0; i--) {
      const j = randInt(0, i);
      [copy[i], copy[j]] = [copy[j], copy[i]];
    }
    return copy;
  };

  const same = (a: Point, b: Point) => a.x === b.x && a.y === b.y;

  const manhattan = (a: Point, b: Point) =>
    Math.abs(a.x - b.x) + Math.abs(a.y - b.y);

  const inBounds = (p: Point) =>
    p.x >= 0 && p.x < width && p.y >= 0 && p.y < height;

  const neighbors = (k: string): string[] => {
    const p = fromKey(k);
    return [
      { x: p.x + 1, y: p.y },
      { x: p.x - 1, y: p.y },
      { x: p.x, y: p.y + 1 },
      { x: p.x, y: p.y - 1 },
    ]
      .filter(inBounds)
      .map(key);
  };

  const isEdgeGoal = (p: Point) =>
    p.x === 0 || p.x === width - 1 || p.y === 0 || p.y === height - 1;

  const isCenterGoal = (p: Point) =>
    p.x >= CENTER_MIN_X &&
    p.x <= CENTER_MAX_X &&
    p.y >= CENTER_MIN_Y &&
    p.y <= CENTER_MAX_Y;

  const isValidGoal = (p: Point) => isEdgeGoal(p) || isCenterGoal(p);

  const pickGoal = (): Goal => {
    const useCenter =
      goalMode === "center" || (goalMode === "mixed" && Math.random() < 0.3);

    if (useCenter) {
      return {
        x: randInt(CENTER_MIN_X, CENTER_MAX_X),
        y: randInt(CENTER_MIN_Y, CENTER_MAX_Y),
        type: "center",
      };
    }

    const side = pick(["left", "right", "top", "bottom"] as const);

    if (side === "left") {
      return { x: 0, y: randInt(1, height - 2), type: "left" };
    }

    if (side === "right") {
      return { x: width - 1, y: randInt(1, height - 2), type: "right" };
    }

    if (side === "top") {
      return { x: randInt(1, width - 2), y: 0, type: "top" };
    }

    return { x: randInt(1, width - 2), y: height - 1, type: "bottom" };
  };

  const getEdgeCells = (): Point[] => {
    const cells: Point[] = [];

    for (let x = 0; x < width; x++) {
      cells.push({ x, y: 0 });
      cells.push({ x, y: height - 1 });
    }

    for (let y = 1; y < height - 1; y++) {
      cells.push({ x: 0, y });
      cells.push({ x: width - 1, y });
    }

    return cells;
  };

  const pickSpawns = (goal: Point): Point[] | null => {
    const candidates = shuffle(getEdgeCells())
      .filter((p) => !same(p, goal))
      .filter((p) => manhattan(p, goal) >= 7);

    const selected: Point[] = [];
    const used = new Set<string>([key(goal)]);

    for (const p of candidates) {
      if (selected.length >= spawnCount) break;
      if (used.has(key(p))) continue;

      const farEnough = selected.every((s) => manhattan(s, p) >= 3);
      if (!farEnough) continue;

      selected.push(p);
      used.add(key(p));
    }

    if (selected.length !== spawnCount) return null;

    return selected.sort((a, b) => manhattan(b, goal) - manhattan(a, goal));
  };

  const lineKeys = (a: Point, b: Point): string[] | null => {
    if (a.x !== b.x && a.y !== b.y) return null;

    const result: string[] = [];
    const dx = Math.sign(b.x - a.x);
    const dy = Math.sign(b.y - a.y);

    let x = a.x;
    let y = a.y;

    result.push(`${x},${y}`);

    while (x !== b.x || y !== b.y) {
      x += dx;
      y += dy;
      result.push(`${x},${y}`);
    }

    return result;
  };

  const routeKeys = (points: Point[]): string[] | null => {
    const cleaned = points.filter((p, i) => {
      if (!inBounds(p)) return false;
      if (i === 0) return true;
      return !same(p, points[i - 1]);
    });

    if (cleaned.length < 2) return null;

    const result: string[] = [];

    for (let i = 0; i < cleaned.length - 1; i++) {
      const segment = lineKeys(cleaned[i], cleaned[i + 1]);
      if (!segment) return null;

      if (i === 0) result.push(...segment);
      else result.push(...segment.slice(1));
    }

    return result;
  };

  const generateCandidateRoutes = (start: Point, attach: Point): Point[][] => {
    const candidates: Point[][] = [];

    candidates.push([start, { x: attach.x, y: start.y }, attach]);
    candidates.push([start, { x: start.x, y: attach.y }, attach]);

    for (let i = 0; i < 28; i++) {
      const mx = randInt(1, width - 2);
      const my = randInt(1, height - 2);

      candidates.push([
        start,
        { x: mx, y: start.y },
        { x: mx, y: attach.y },
        attach,
      ]);

      candidates.push([
        start,
        { x: start.x, y: my },
        { x: attach.x, y: my },
        attach,
      ]);

      candidates.push([
        start,
        { x: mx, y: start.y },
        { x: mx, y: my },
        { x: attach.x, y: my },
        attach,
      ]);

      candidates.push([
        start,
        { x: start.x, y: my },
        { x: mx, y: my },
        { x: mx, y: attach.y },
        attach,
      ]);
    }

    return shuffle(candidates);
  };

  const hasTwoByTwoBlock = (path: Set<string>) => {
    for (let y = 0; y < height - 1; y++) {
      for (let x = 0; x < width - 1; x++) {
        const cells = [
          `${x},${y}`,
          `${x + 1},${y}`,
          `${x},${y + 1}`,
          `${x + 1},${y + 1}`,
        ];

        if (cells.every((c) => path.has(c))) return true;
      }
    }

    return false;
  };

  const degreeInPath = (k: string, path: Set<string>) =>
    neighbors(k).filter((n) => path.has(n)).length;

  const isConnected = (path: Set<string>) => {
    const cells = [...path];
    if (cells.length === 0) return false;

    const visited = new Set<string>([cells[0]]);
    const queue = [cells[0]];

    while (queue.length > 0) {
      const current = queue.shift()!;

      for (const n of neighbors(current)) {
        if (!path.has(n) || visited.has(n)) continue;

        visited.add(n);
        queue.push(n);
      }
    }

    return visited.size === path.size;
  };

  const isValidRoute = (
    keys: string[] | null,
    attachKey: string,
    currentPath: Set<string>,
    allSpawnKeys: Set<string>,
    ownSpawnKey: string
  ) => {
    if (!keys || keys.length < 2) return false;
    if (keys[0] !== ownSpawnKey) return false;
    if (keys[keys.length - 1] !== attachKey) return false;
    if (!currentPath.has(attachKey)) return false;

    if (new Set(keys).size !== keys.length) return false;

    const lastNewIndex = keys.length - 2;

    for (let i = 0; i <= lastNewIndex; i++) {
      const k = keys[i];

      if (currentPath.has(k)) return false;
      if (allSpawnKeys.has(k) && k !== ownSpawnKey) return false;

      for (const n of neighbors(k)) {
        if (!currentPath.has(n)) continue;

        const isAllowedFinalContact = n === attachKey && i === lastNewIndex;
        if (!isAllowedFinalContact) return false;
      }
    }

    const testPath = new Set(currentPath);
    for (let i = 0; i <= lastNewIndex; i++) {
      testPath.add(keys[i]);
    }

    if (hasTwoByTwoBlock(testPath)) return false;

    return true;
  };

  const getAttachCandidates = (
    currentPath: Set<string>,
    goalKey: string,
    allSpawnKeys: Set<string>,
    firstRoute: boolean
  ) => {
    if (firstRoute) return [goalKey];

    return shuffle([...currentPath]).filter((k) => {
      if (k === goalKey) return false;
      if (allSpawnKeys.has(k)) return false;

      return degreeInPath(k, currentPath) <= 2;
    });
  };

  const findRoute = (
    start: Point,
    currentPath: Set<string>,
    goalKey: string,
    allSpawnKeys: Set<string>,
    firstRoute: boolean
  ) => {
    const ownSpawnKey = key(start);
    const attachCandidates = getAttachCandidates(
      currentPath,
      goalKey,
      allSpawnKeys,
      firstRoute
    );

    if (attachCandidates.length === 0) return null;

    for (let attempt = 0; attempt < maxRouteAttempts; attempt++) {
      const attachKey = pick(attachCandidates);
      const attach = fromKey(attachKey);
      const candidates = generateCandidateRoutes(start, attach);

      for (const points of candidates) {
        const keys = routeKeys(points);

        if (
          isValidRoute(
            keys,
            attachKey,
            currentPath,
            allSpawnKeys,
            ownSpawnKey
          )
        ) {
          return { keys: keys!, attachKey };
        }
      }
    }

    return null;
  };

  const validateFinalMap = (map: {
    goal: Goal;
    spawns: Point[];
    path: Set<string>;
  }) => {
    if (!isValidGoal(map.goal)) return false;
    if (hasTwoByTwoBlock(map.path)) return false;
    if (!isConnected(map.path)) return false;

    const goalKey = key(map.goal);
    if (degreeInPath(goalKey, map.path) !== 1) return false;

    for (const spawn of map.spawns) {
      const spawnKey = key(spawn);

      if (!map.path.has(spawnKey)) return false;
      if (degreeInPath(spawnKey, map.path) !== 1) return false;
    }

    return true;
  };

  const getMerges = (
    path: Set<string>,
    goalKey: string,
    spawnKeys: Set<string>
  ): Point[] => {
    return [...path]
      .filter((k) => {
        if (k === goalKey) return false;
        if (spawnKeys.has(k)) return false;

        return degreeInPath(k, path) >= 3;
      })
      .map(fromKey);
  };

  for (let attempt = 1; attempt <= maxMapAttempts; attempt++) {
    const goal = pickGoal();
    const spawns = pickSpawns(goal);

    if (!spawns) continue;

    const goalKey = key(goal);
    const allSpawnKeys = new Set(spawns.map(key));
    const path = new Set<string>([goalKey]);

    let failed = false;

    for (let i = 0; i < spawns.length; i++) {
      const route = findRoute(
        spawns[i],
        path,
        goalKey,
        allSpawnKeys,
        i === 0
      );

      if (!route) {
        failed = true;
        break;
      }

      for (const k of route.keys.slice(0, -1)) {
        path.add(k);
      }
    }

    if (failed) continue;

    const map = {
      width,
      height,
      goal,
      spawns,
      path,
      merges: getMerges(path, goalKey, allSpawnKeys),
      attempts: attempt,
    };

    if (!validateFinalMap(map)) continue;

    return {
      ...map,
      path: [...path].map(fromKey),
    };
  }

  throw new Error("조건을 만족하는 맵 생성에 실패했습니다.");
}
```
