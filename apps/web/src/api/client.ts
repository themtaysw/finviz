import type { NodeDetails, NodeSummary, Page } from './types'

export const ROOT_ID = 0
export const PAGE_SIZE = 100

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

async function getJson<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(new URL(`/api${path}`, window.location.origin), {
    signal,
    headers: { Accept: 'application/json' },
  })

  if (!response.ok) {
    throw new ApiError(response.status, `GET ${path} failed with ${response.status}`)
  }

  return (await response.json()) as T
}

export function fetchChildren(parentId: number, page: number, signal?: AbortSignal) {
  const query = new URLSearchParams({
    offset: String(page * PAGE_SIZE),
    limit: String(PAGE_SIZE),
  })
  const path = parentId === ROOT_ID ? '/nodes/roots' : `/nodes/${parentId}/children`

  return getJson<Page<NodeSummary>>(`${path}?${query}`, signal)
}

export function fetchLargestChildren(id: number, limit: number, signal?: AbortSignal) {
  const query = new URLSearchParams({ order: 'size', limit: String(limit) })
  return getJson<Page<NodeSummary>>(`/nodes/${id}/children?${query}`, signal)
}

export function fetchNode(id: number, signal?: AbortSignal) {
  return getJson<NodeDetails>(`/nodes/${id}`, signal)
}
