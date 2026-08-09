import './Table.css'

export default function Table({ columns, data, loading = false, emptyMessage = 'No records found', onRowClick }) {
  if (loading) {
    return (
      <div className="table-wrapper">
        <table className="table">
          <thead>
            <tr>
              {columns.map(col => (
                <th key={col.key} style={{ width: col.width }}>{col.label}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {Array.from({ length: 5 }).map((_, i) => (
              <tr key={i}>
                {columns.map(col => (
                  <td key={col.key}>
                    <div className="skeleton" style={{ height: '16px', borderRadius: '4px' }} />
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    )
  }

  return (
    <div className="table-wrapper">
      <table className="table">
        <thead>
          <tr>
            {columns.map(col => (
              <th key={col.key} style={{ width: col.width }} className={col.align ? `text-${col.align}` : ''}>
                {col.label}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {data.length === 0 ? (
            <tr>
              <td colSpan={columns.length} className="table__empty">
                {emptyMessage}
              </td>
            </tr>
          ) : (
            data.map((row, i) => (
              <tr
                key={row.id || row.appointmentId || row.patientId || i}
                className={onRowClick ? 'table__row--clickable' : ''}
                onClick={() => onRowClick?.(row)}
              >
                {columns.map(col => (
                  <td key={col.key} className={col.align ? `text-${col.align}` : ''}>
                    {col.render ? col.render(row) : row[col.key]}
                  </td>
                ))}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  )
}
