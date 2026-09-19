export class ApiError<TBody = unknown> extends Error {
  readonly status: number
  readonly body: TBody | undefined

  constructor(status: number, message: string, body?: TBody) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.body = body
  }
}

export type ErrorType<TBody> = ApiError<TBody>

export async function fetcher<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(new URL(url, window.location.origin), {
    ...init,
    headers: { Accept: 'application/json', ...init?.headers },
  })

  if (!response.ok) {
    const isJson = response.headers.get('Content-Type')?.includes('json') ?? false
    throw new ApiError(
      response.status,
      `${init?.method ?? 'GET'} ${url} failed with ${response.status}`,
      isJson ? await response.json() : undefined,
    )
  }

  return (await response.json()) as T
}
