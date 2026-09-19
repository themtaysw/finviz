import { useQuery } from '@tanstack/react-query'
import { NodeDetails } from './details/NodeDetails'
import { formatCount } from './lib/format'
import { useSelectedNodeId } from './lib/useSelectedNodeId'
import { childrenPageQuery } from './tree/childrenPageQuery'
import { Tree } from './tree/Tree'
import { ROOT_ID } from './tree/treeModel'
import styles from './App.module.css'

export function App() {
  const [selectedId, select] = useSelectedNodeId()
  const { data: roots } = useQuery(childrenPageQuery(ROOT_ID, 0))
  const root = roots?.items[0]
  const shownId = selectedId ?? root?.id

  return (
    <div className={styles.app}>
      <header className={styles.header}>
        <h1>ImageNet taxonomy</h1>
        {root && <span className={styles.subtitle}>{formatCount(root.size + 1)} categories</span>}
      </header>
      <main className={styles.main}>
        <aside className={styles.sidebar}>
          <Tree selectedId={selectedId} onSelect={select} />
        </aside>
        <section className={styles.content}>
          {shownId !== undefined && <NodeDetails id={shownId} onSelect={select} />}
        </section>
      </main>
    </div>
  )
}
