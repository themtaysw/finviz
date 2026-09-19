import type { ApiError } from '../api/fetcher'
import type { PageOfNodeSummary } from '../api/generated/model'
import { getGetChildrenQueryOptions, getGetRootsQueryOptions } from '../api/generated/taxonomy'
import { PAGE_SIZE, ROOT_ID } from './treeModel'

export function childrenPageQuery(parentId: number, page: number) {
  const params = { offset: page * PAGE_SIZE, limit: PAGE_SIZE }
  return parentId === ROOT_ID
    ? getGetRootsQueryOptions<PageOfNodeSummary, ApiError>(params)
    : getGetChildrenQueryOptions<PageOfNodeSummary, ApiError>(parentId, params)
}
