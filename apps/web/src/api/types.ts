export type NodeSummary = {
  id: number
  label: string
  size: number
  childCount: number
}

export type NodeAncestor = {
  id: number
  label: string
  index: number
  childCount: number
}

export type NodeDetails = NodeSummary & {
  path: string
  depth: number
  index: number
  ancestors: NodeAncestor[]
}

export type Page<T> = {
  items: T[]
  total: number
  offset: number
  limit: number
}
