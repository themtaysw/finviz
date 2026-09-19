import { memo, type CSSProperties } from 'react'
import type { NodeSummary } from '../api/generated/model'
import { formatCount, splitLabel } from '../lib/format'
import { rowKey, type Expansion, type TreeRow } from './treeModel'
import styles from './Tree.module.css'

type TreeItemProps = {
  id: string
  row: TreeRow
  offset: number
  node: NodeSummary | undefined
  isActive: boolean
  isExpanded: boolean
  isSelected: boolean
  onToggle: (id: number, expansion: Expansion) => void
  onSelect: (id: number) => void
}

export const TreeItem = memo(function TreeItem({
  id,
  row,
  offset,
  node,
  isActive,
  isExpanded,
  isSelected,
  onToggle,
  onSelect,
}: TreeItemProps) {
  const hasChildren = (node?.childCount ?? 0) > 0

  return (
    <div
      id={id}
      role="treeitem"
      aria-label={node?.label ?? 'Loading'}
      aria-level={row.depth + 1}
      aria-setsize={row.siblingCount}
      aria-posinset={row.index + 1}
      aria-expanded={hasChildren ? isExpanded : undefined}
      aria-selected={isSelected}
      aria-busy={node ? undefined : true}
      data-key={rowKey(row.parentId, row.index)}
      data-active={isActive}
      className={styles.row}
      style={{ '--depth': row.depth, transform: `translateY(${offset}px)` } as CSSProperties}
    >
      {node ? (
        <>
          {hasChildren ? (
            <button
              type="button"
              tabIndex={-1}
              className={styles.toggle}
              aria-label={isExpanded ? 'Collapse' : 'Expand'}
              onClick={() =>
                onToggle(node.id, {
                  parentId: row.parentId,
                  index: row.index,
                  childCount: node.childCount,
                })
              }
            >
              <svg viewBox="0 0 16 16" aria-hidden="true" data-expanded={isExpanded}>
                <path d="M6 4l4 4-4 4" />
              </svg>
            </button>
          ) : (
            <span className={styles.toggle} />
          )}
          <button
            type="button"
            tabIndex={-1}
            className={styles.label}
            onClick={() => onSelect(node.id)}
          >
            <Label text={node.label} />
          </button>
          {node.size > 0 && <span className={styles.count}>{formatCount(node.size)}</span>}
        </>
      ) : (
        <span className={styles.placeholder} />
      )}
    </div>
  )
})

function Label({ text }: { text: string }) {
  const [name, ...synonyms] = splitLabel(text)
  return (
    <span title={text}>
      {name}
      {synonyms.length > 0 && <span className={styles.synonyms}> · {synonyms.join(', ')}</span>}
    </span>
  )
}
