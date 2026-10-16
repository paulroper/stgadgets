import { lazy, Suspense, type ComponentType } from "react";
import { GenericLayout } from "./GenericLayout";

// Every pack's UI (packs/<id>/ui/layout.tsx), each built into its own chunk and loaded on first use.
// Pack UIs may import only @stgadgets/sdk, @stgadgets/gadgets, react and their own files (enforced in vite.config.ts).
const modules = import.meta.glob<{ default: ComponentType }>("../../../../packs/*/ui/layout.tsx");

const layouts = new Map(
  Object.entries(modules).map(([path, load]) => [path.match(/packs\/([^/]+)\/ui\/layout\.tsx$/)![1], lazy(load)]),
);

/** The pack's own layout, or the generic one if the pack has no UI. */
export function PackLayout({ pack }: { pack: string | null }) {
  const Layout = (pack && layouts.get(pack)) || GenericLayout;
  return (
    <Suspense fallback={null}>
      <Layout />
    </Suspense>
  );
}
