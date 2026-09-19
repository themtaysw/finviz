import { useGetChildren, useGetNode } from '../api/generated/taxonomy'
import type { NodeDetails as Node, NodeSummary } from '../api/generated/model'
import { formatCount, formatPercent, splitLabel } from '../lib/format'
import styles from './NodeDetails.module.css'

const LARGEST_LIMIT = 12

type NodeDetailsProps = {
  id: number
  onSelect: (id: number) => void
}

export function NodeDetails({ id, onSelect }: NodeDetailsProps) {
  const { data: node, error } = useGetNode(id)

  if (error) return <p className={styles.message}>Couldn’t load this category.</p>
  if (!node) return <p className={styles.message}>Loading…</p>

  const [name, ...synonyms] = splitLabel(node.label)

  return (
    <article className={styles.details}>
      <nav aria-label="Breadcrumb">
        <ol className={styles.breadcrumbs}>
          {node.ancestors.map((ancestor) => (
            <li key={ancestor.id}>
              <button type="button" onClick={() => onSelect(ancestor.id)}>
                {splitLabel(ancestor.label)[0]}
              </button>
            </li>
          ))}
        </ol>
      </nav>

      <h2 className={styles.title}>{name}</h2>
      {synonyms.length > 0 && <p className={styles.synonyms}>Also: {synonyms.join(', ')}</p>}

      <dl className={styles.stats}>
        <Stat label="Descendants" value={formatCount(node.size)} />
        <Stat label="Subcategories" value={formatCount(node.childCount)} />
        <Stat label="Level" value={String(node.depth)} />
      </dl>

      {node.childCount > 0 ? (
        <LargestChildren node={node} onSelect={onSelect} />
      ) : (
        <p className={styles.message}>This is a leaf category.</p>
      )}
    </article>
  )
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  )
}

function LargestChildren({ node, onSelect }: { node: Node; onSelect: (id: number) => void }) {
  const { data } = useGetChildren(node.id, { order: 'size', limit: LARGEST_LIMIT })

  return (
    <section>
      <h3 className={styles.sectionTitle}>
        Largest subcategories
        {node.childCount > LARGEST_LIMIT && (
          <span>
            {' '}
            · top {LARGEST_LIMIT} of {formatCount(node.childCount)}
          </span>
        )}
      </h3>
      <ul className={styles.bars}>
        {data?.items.map((child) => (
          <ShareBar key={child.id} child={child} total={node.size} onSelect={onSelect} />
        ))}
      </ul>
    </section>
  )
}

function ShareBar({
  child,
  total,
  onSelect,
}: {
  child: NodeSummary
  total: number
  onSelect: (id: number) => void
}) {
  const share = (child.size + 1) / total

  return (
    <li>
      <button type="button" className={styles.bar} onClick={() => onSelect(child.id)}>
        <span className={styles.barLabel}>{splitLabel(child.label)[0]}</span>
        <span className={styles.barValue}>
          {formatCount(child.size + 1)} · {formatPercent(share)}
        </span>
        <span className={styles.barFill} style={{ inlineSize: `${share * 100}%` }} />
      </button>
    </li>
  )
}
