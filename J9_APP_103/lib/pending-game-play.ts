type PendingGamePlay = {
  url: string;
  title: string;
};

let pendingGamePlay: PendingGamePlay | null = null;

export function setPendingGamePlay(payload: PendingGamePlay) {
  pendingGamePlay = payload;
}

export function consumePendingGamePlay(): PendingGamePlay | null {
  const current = pendingGamePlay;
  pendingGamePlay = null;
  return current;
}
