import { useCallback, useEffect, useState } from 'react'
import { AlertTriangle, CheckCircle2, ClipboardCheck, FileClock, RefreshCw, XCircle } from 'lucide-react'
import Button from '../../../components/Button'
import { triageApi } from '../services/triageApi'
import './TriageReviewPage.css'

export default function TriageReviewPage() {
  const [workflows, setWorkflows] = useState([])
  const [selected, setSelected] = useState(null)
  const [auditEvents, setAuditEvents] = useState([])
  const [loading, setLoading] = useState(true)
  const [acting, setActing] = useState(false)
  const [error, setError] = useState('')

  const loadQueue = useCallback(async () => {
    setLoading(true); setError('')
    try {
      const queue = await triageApi.getPending()
      setWorkflows(queue)
      if (!selected && queue.length) await selectWorkflow(queue[0])
      if (!queue.length) { setSelected(null); setAuditEvents([]) }
    } catch (err) { setError(err.response?.data?.message || 'Unable to load the clinical review queue.') }
    finally { setLoading(false) }
  // selected intentionally only controls initial selection; refreshing must not discard reviewer context.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const selectWorkflow = async workflow => {
    setSelected(workflow); setAuditEvents([]); setError('')
    try {
      const [details, events] = await Promise.all([triageApi.getWorkflow(workflow.workflowId), triageApi.getAuditEvents(workflow.workflowId)])
      setSelected(details); setAuditEvents(events)
    } catch (err) { setError(err.response?.data?.message || 'Unable to load workflow details.') }
  }

  useEffect(() => { loadQueue() }, [loadQueue])

  const review = async decision => {
    if (!selected) return
    const note = window.prompt(`Optional clinical note for ${decision}:`) ?? ''
    setActing(true); setError('')
    try {
      await triageApi.review(selected.workflowId, decision, note)
      await loadQueue()
    } catch (err) { setError(err.response?.data?.message || 'Unable to record the clinical decision.') }
    finally { setActing(false) }
  }

  return <div className="page-wrapper triage-review">
    <div className="page-header triage-review__heading">
      <div><h1 className="page-title">SafeTriage Clinical Review</h1><p className="page-subtitle">Decision-support workflows requiring authorised clinical oversight</p></div>
      <Button variant="secondary" icon={RefreshCw} loading={loading} onClick={loadQueue}>Refresh queue</Button>
    </div>
    <div className="triage-review__notice"><AlertTriangle size={18}/><span>These are AI-assisted decision-support records, not diagnoses. Review the information and make the clinical decision independently.</span></div>
    {error && <div className="triage-review__error"><XCircle size={17}/>{error}</div>}
    <div className="triage-review__layout">
      <section className="glass-card triage-review__queue">
        <div className="triage-review__section-title"><FileClock size={18}/><h3>Pending review</h3><span>{workflows.length}</span></div>
        {loading ? <p className="text-muted">Loading workflows…</p> : workflows.length === 0 ? <div className="triage-review__empty"><CheckCircle2 size={30}/><p>No pending emergency workflows.</p></div> : workflows.map(workflow => <button key={workflow.workflowId} className={`triage-review__queue-item ${selected?.workflowId === workflow.workflowId ? 'triage-review__queue-item--active' : ''}`} onClick={() => selectWorkflow(workflow)}><strong>Workflow #{workflow.workflowId}</strong><span>{workflow.triageLevel} · {workflow.uncertaintyState}</span><small>{workflow.redFlags?.join(', ') || 'Clinical review required'}</small></button>)}
      </section>
      <section className="glass-card triage-review__details">
        {!selected ? <div className="triage-review__empty"><ClipboardCheck size={34}/><p>Select a workflow to review its safety summary.</p></div> : <>
          <div className="triage-review__section-title"><AlertTriangle size={18}/><h3>Workflow #{selected.workflowId}</h3><span className="triage-review__level">{selected.triageLevel}</span></div>
          <p className="triage-review__message">{selected.patientMessage}</p>
          <Info title="Configured safety flags" items={selected.redFlags} fallback="No configured red-flag phrase is recorded." />
          <Info title="Information limitations" items={selected.missingInformation} fallback="No limitations were recorded." />
          <section className="triage-review__audit"><h4>Auditable workflow events</h4>{auditEvents.map(event => <div key={`${event.stage}-${event.createdAt}`}><strong>{event.stage}</strong><span>{event.eventType}</span><time>{new Date(event.createdAt).toLocaleString()}</time></div>)}</section>
          <div className="triage-review__actions"><Button variant="primary" icon={CheckCircle2} loading={acting} onClick={() => review('Approved')}>Approve escalation</Button><Button variant="danger" icon={XCircle} disabled={acting} onClick={() => review('Rejected')}>Reject</Button><Button variant="secondary" icon={ClipboardCheck} disabled={acting} onClick={() => review('RevisionRequested')}>Request revision</Button></div>
        </>}
      </section>
    </div>
  </div>
}

function Info({ title, items, fallback }) { return <section className="triage-review__info"><h4>{title}</h4>{items?.length ? <ul>{items.map(item => <li key={item}>{item}</li>)}</ul> : <p>{fallback}</p>}</section> }
