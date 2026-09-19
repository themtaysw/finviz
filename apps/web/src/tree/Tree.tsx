import { useVirtualizer } from '@tanstack/react-virtual'
import {
  useCallback,
  useEffect,
  useId,
  useMemo,
  useReducer,
  useRef,
  useState,
  type KeyboardEvent,
  type MouseEvent,
} from 'react'
import { useGetNode } from '../api/generated/taxonomy'
import {
  indexRows,
  ROOT_ID,
  rowKey,
  treeReducer,
  type Expansion,
  type ExpandedNodes,
} from './treeModel'
import { TreeItem } from './TreeItem'
import { useRowNodes, useTreeRows } from './useTreeRows'
import styles from './Tree.module.css'

const ROW_HEIGHT = 28

type TreeProps = {
  selectedId: number | null
  onSelect: (id: number) => void
}

const noneExpanded: ExpandedNodes = new Map()

export function Tree({ selectedId, onSelect }: TreeProps) {
  const [expanded, dispatch] = useReducer(treeReducer, noneExpanded)
  const [activeKey, setActiveKey] = useState<string | null>(null)
  const { rows, isPending, error, retry } = useTreeRows(expanded)
  const rowIndex = useMemo(() => indexRows(rows), [rows])
  const viewportRef = useRef<HTMLDivElement>(null)
  const treeId = useId()

  const virtualizer = useVirtualizer({
    count: rows.length,
    getScrollElement: () => viewportRef.current,
    estimateSize: () => ROW_HEIGHT,
    getItemKey: (i) => rowKey(rows[i]!.parentId, rows[i]!.index),
    overscan: 10,
  })
  const items = virtualizer.getVirtualItems()
  const nodeAt = useRowNodes(rows, items[0]?.index ?? 0, items.at(-1)?.index ?? -1)

  const firstRoot = rows.length === 1 ? nodeAt(rows[0]!) : undefined
  const openedFirstRoot = useRef(false)
  useEffect(() => {
    if (!firstRoot || openedFirstRoot.current) return
    openedFirstRoot.current = true
    if (firstRoot.childCount > 0) {
      dispatch({
        type: 'expand',
        id: firstRoot.id,
        expansion: { parentId: ROOT_ID, index: 0, childCount: firstRoot.childCount },
      })
    }
  }, [firstRoot])

  const { data: selected } = useGetNode(selectedId ?? 0, {
    query: { enabled: selectedId !== null },
  })

  useEffect(() => {
    if (selected) dispatch({ type: 'reveal', node: selected })
  }, [selected])

  const scrolledTo = useRef<number | null>(null)
  useEffect(() => {
    if (!selected || scrolledTo.current === selected.id) return
    const key = rowKey(selected.ancestors.at(-1)?.id ?? ROOT_ID, selected.index)
    const index = rowIndex.get(key)
    if (index !== undefined) {
      virtualizer.scrollToIndex(index)
      setActiveKey(key)
      scrolledTo.current = selected.id
    }
  }, [selected, rowIndex, virtualizer])

  const toggle = useCallback(
    (id: number, expansion: Expansion) => dispatch({ type: 'toggle', id, expansion }),
    [],
  )

  const activeIndex = activeKey === null ? -1 : (rowIndex.get(activeKey) ?? -1)

  const moveTo = (index: number) => {
    const i = Math.max(0, Math.min(index, rows.length - 1))
    const target = rows[i]
    if (!target) return
    setActiveKey(rowKey(target.parentId, target.index))
    virtualizer.scrollToIndex(i)
  }

  const onMouseDown = (event: MouseEvent<HTMLDivElement>) => {
    const row = (event.target as HTMLElement).closest<HTMLElement>('[role="treeitem"]')
    if (!row?.dataset.key) return
    event.preventDefault()
    viewportRef.current?.focus({ preventScroll: true })
    setActiveKey(row.dataset.key)
  }

  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    const current = Math.max(activeIndex, 0)
    const row = rows[current]
    const node = row && nodeAt(row)
    const pageRows = Math.max(
      1,
      Math.floor((viewportRef.current?.clientHeight ?? 0) / ROW_HEIGHT) - 1,
    )

    switch (event.key) {
      case 'ArrowDown':
        moveTo(activeIndex < 0 ? 0 : current + 1)
        break
      case 'ArrowUp':
        moveTo(current - 1)
        break
      case 'PageDown':
        moveTo(current + pageRows)
        break
      case 'PageUp':
        moveTo(current - pageRows)
        break
      case 'Home':
        moveTo(0)
        break
      case 'End':
        moveTo(rows.length - 1)
        break
      case 'ArrowRight':
        if (!row || !node || node.childCount === 0) break
        if (expanded.has(node.id)) moveTo(current + 1)
        else
          toggle(node.id, { parentId: row.parentId, index: row.index, childCount: node.childCount })
        break
      case 'ArrowLeft': {
        if (!row) break
        if (node && expanded.has(node.id)) {
          dispatch({ type: 'collapse', id: node.id })
          break
        }
        const parent = expanded.get(row.parentId)
        if (parent) moveTo(rowIndex.get(rowKey(parent.parentId, parent.index)) ?? current)
        break
      }
      case 'Enter':
      case ' ':
        if (node) onSelect(node.id)
        break
      default:
        return
    }
    event.preventDefault()
  }

  if (error) {
    return (
      <div className={styles.message} role="alert">
        <p>Couldn’t load the taxonomy.</p>
        <button type="button" onClick={() => void retry()}>
          Try again
        </button>
      </div>
    )
  }
  if (isPending) return <p className={styles.message}>Loading…</p>

  const activeRowRendered = items.some((item) => item.index === activeIndex)

  return (
    <div
      ref={viewportRef}
      role="tree"
      aria-label="Categories"
      aria-activedescendant={activeRowRendered ? `${treeId}-${activeKey}` : undefined}
      tabIndex={0}
      className={styles.viewport}
      onKeyDown={onKeyDown}
      onMouseDown={onMouseDown}
    >
      <div className={styles.tree} style={{ height: virtualizer.getTotalSize() }}>
        {items.map((item) => {
          const row = rows[item.index]!
          const node = nodeAt(row)
          return (
            <TreeItem
              key={item.key}
              id={`${treeId}-${item.key}`}
              row={row}
              offset={item.start}
              node={node}
              isActive={item.index === activeIndex}
              isExpanded={node ? expanded.has(node.id) : false}
              isSelected={node?.id === selectedId}
              onToggle={toggle}
              onSelect={onSelect}
            />
          )
        })}
      </div>
    </div>
  )
}
