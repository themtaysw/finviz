import { expect, test, type Page } from '@playwright/test'

const ROOT = 'ImageNet 2011 Fall Release'

const tree = (page: Page) => page.getByRole('tree', { name: 'Categories' })
const searchBox = (page: Page) => page.getByRole('combobox', { name: 'Search categories' })
const details = (page: Page) => page.getByRole('heading', { level: 2 })

async function expectTopLevelLoaded(page: Page) {
  await expect(tree(page).getByRole('treeitem', { level: 2 })).toHaveCount(9)
  await expect(tree(page).getByRole('treeitem', { name: 'Loading' })).toHaveCount(0)
}

type NodeDetails = {
  id: number
  label: string
  ancestors: { id: number; label: string; childCount: number }[]
}

test('opens on the top-level categories', async ({ page }) => {
  await page.goto('/')

  await expect(tree(page).getByRole('treeitem', { name: ROOT })).toHaveAttribute(
    'aria-expanded',
    'true',
  )
  await expectTopLevelLoaded(page)
  await expect(details(page)).toHaveText(ROOT)
})

test('finds a category by search and reveals it in the tree', async ({ page }) => {
  await page.goto('/')
  await expect(details(page)).toHaveText(ROOT)

  await page.keyboard.press('/')
  await page.keyboard.type('dog')
  await expect(page.getByRole('option').first()).toHaveAccessibleName(
    'dog, domestic dog, Canis familiaris',
  )
  await page.keyboard.press('Enter')

  await expect(page).toHaveURL(/\?node=\d+$/)
  await expect(details(page)).toHaveText('dog')
  await expect(page.getByRole('navigation', { name: 'Breadcrumb' })).toContainText(
    'domestic animal',
  )
  await expect(
    tree(page).getByRole('treeitem', {
      name: 'dog, domestic dog, Canis familiaris',
      selected: true,
    }),
  ).toBeInViewport()
})

test('aborts a search overtaken by a newer query', async ({ page }) => {
  const isSearchFor = (query: string) => (url: URL) =>
    url.pathname === '/api/search' && url.searchParams.get('q') === query

  await page.route(isSearchFor('dog'), async (route) => {
    await new Promise((resolve) => setTimeout(resolve, 1_500))
    await route.continue().catch(() => undefined)
  })
  const aborted = page.waitForEvent('requestfailed', (request) =>
    isSearchFor('dog')(new URL(request.url())),
  )

  await page.goto('/')
  await searchBox(page).fill('dog')
  await page.waitForRequest((request) => isSearchFor('dog')(new URL(request.url())))
  await searchBox(page).fill('dogwood')

  expect((await aborted).failure()?.errorText).toContain('ERR_ABORTED')
  await expect(page.getByRole('option').first()).toHaveAccessibleName(/dogwood/)
})

test('scrolls a deep link into a huge list and fetches only the pages around it', async ({
  page,
  request,
}) => {
  const node = (await (await request.get('/api/nodes/60000')).json()) as NodeDetails
  const parent = node.ancestors.at(-1)!
  const pagesFetched: string[] = []
  page.on('request', (request) => {
    const url = new URL(request.url())
    if (url.pathname === `/api/nodes/${parent.id}/children`) {
      pagesFetched.push(url.searchParams.get('offset')!)
    }
  })

  await page.goto(`/?node=${node.id}`)

  await expect(
    tree(page).getByRole('treeitem', { name: node.label, selected: true }),
  ).toBeInViewport()
  expect(parent.childCount).toBeGreaterThan(2_000)
  expect(pagesFetched.length).toBeLessThanOrEqual(3)
  expect(await tree(page).getByRole('treeitem').count()).toBeLessThan(80)
})

test('can be used with the keyboard alone', async ({ page }) => {
  await page.goto('/')
  await expectTopLevelLoaded(page)

  await page.keyboard.press('Tab')
  await expect(searchBox(page)).toBeFocused()
  await page.keyboard.press('Tab')
  await expect(tree(page)).toBeFocused()

  await page.keyboard.press('ArrowDown')
  await page.keyboard.press('ArrowDown')
  await page.keyboard.press('ArrowRight')
  await expect(
    tree(page).getByRole('treeitem', { name: 'plant, flora, plant life' }),
  ).toHaveAttribute('aria-expanded', 'true')
  await expect(tree(page).getByRole('treeitem', { name: 'phytoplankton' })).toBeVisible()
  await page.keyboard.press('ArrowRight')
  await page.keyboard.press('Enter')

  await expect(page).toHaveURL(/\?node=\d+$/)
  await expect(details(page)).toHaveText('phytoplankton')
})
