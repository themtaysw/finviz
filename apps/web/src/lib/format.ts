const counts = new Intl.NumberFormat('en-US')
const percents = new Intl.NumberFormat('en-US', { style: 'percent', maximumFractionDigits: 1 })

export const formatCount = (value: number) => counts.format(value)

export const formatPercent = (value: number) => percents.format(value)

export const splitLabel = (label: string) => label.split(', ')
