import type { ReactNode } from 'react'

export interface Column<Row> {
  header: string
  cell: (row: Row) => ReactNode
  /** Numbers and money read best right-aligned. */
  align?: 'left' | 'right'
}

interface DataTableProps<Row> {
  /** Read out by screen readers; not shown on screen. */
  caption: string
  columns: Column<Row>[]
  rows: Row[]
  rowKey: (row: Row) => string | number
}

export function DataTable<Row>({ caption, columns, rows, rowKey }: DataTableProps<Row>) {
  return (
    // On a narrow screen the table scrolls sideways inside this box, not the whole page.
    <div className="relative overflow-x-auto rounded-lg border border-line bg-white">
      <table className="w-full min-w-max text-left text-sm">
        <caption className="sr-only">{caption}</caption>
        <thead className="border-b border-line bg-paper text-xs tracking-wide text-muted uppercase">
          <tr>
            {columns.map((column) => (
              <th
                key={column.header}
                scope="col"
                className={`px-3 py-2.5 font-semibold ${column.align === 'right' ? 'text-right' : ''}`}
              >
                {column.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-line">
          {rows.map((row) => (
            <tr key={rowKey(row)}>
              {columns.map((column) => (
                <td
                  key={column.header}
                  className={`px-3 py-2.5 align-middle ${column.align === 'right' ? 'text-right font-mono tabular-nums' : ''}`}
                >
                  {column.cell(row)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
