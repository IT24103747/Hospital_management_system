import { useCallback, useEffect, useMemo, useState } from 'react'
import { AlertTriangle, CheckCircle2, ClipboardCheck, FileClock, RefreshCw, Search, XCircle, ShieldCheck } from 'lucide-react'
import Button from '../../../components/Button'
import { triageApi } from '../services/triageApi'
import { useAuth } from '../../auth/AuthContext'
import './TriageReviewPage.css'

export default function TriageReviewPage() {
  const { user } = useAuth()
  const canMakeClinicalDecision = user?.role === 'Doctor'
  const [workflows, setWorkflows] = useState([])
  const [selected, setSelected] = useState(null)
  const [auditEvents, setAuditEvents] = useState([])
  const [loading, setLoading] = useState(true)
  const [acting, setActing] = useState(false)
  const [error, setError] = useState('')
  const [pendingDecision, setPendingDecision] = useState(null)
  const [reviewNote, setReviewNote] = useState('')
  const [success, setSuccess] = useState('')
  const [queueSearch, setQueueSearch] = useState('')

  const filteredWorkflows = useMemo(() => {
    const query = queueSearch.trim().toLowerCase()
    if (!query) return workflows
    return workflows.filter(workflow => [
      workflow.workflowId,
      workflow.triageLevel,
      workflow.uncertaintyState,
      ...(workflow.redFlags || []),
      ...(workflow.urgentFlags || []),
      ...(workflow.clinicalReviewFlags || []),
      ...(workflow.riskFactors || []),
    ].join(' ').toLowerCase().includes(query))
  }, [workflows, queueSearch])

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
    setActing(true); setError('')
    try {
      await triageApi.review(selected.workflowId, decision, reviewNote)
      setSuccess(`${decisionLabel(decision)} was recorded for workflow #${selected.workflowId}.`)
      setPendingDecision(null); setReviewNote('')
      await loadQueue()
    } catch (err) { setError(err.response?.data?.message || 'Unable to record the clinical decision.') }
    finally { setActing(false) }
  }

  return <div className="page-wrapper triage-review">
    <div className="page-header triage-review__heading">
      <div><h1 className="page-title">SafeTriage Clinical Review</h1><p className="page-subtitle">Review AI safety escalations and record the clinician’s independent decision</p></div>
      <Button variant="secondary" icon={RefreshCw} loading={loading} onClick={loadQueue}>Refresh queue</Button>
    </div>
    <div className="triage-review__notice"><ShieldCheck size={18}/><span><strong>Clinical action required:</strong> these records were escalated by SafeTriage. They are decision support, not diagnoses—review the evidence and decide independently.</span></div>
    {!canMakeClinicalDecision && <div className="triage-review__readonly"><ShieldCheck size={18}/><span><strong>Read-only administrator view:</strong> only an authorised doctor can record a clinical triage decision.</span></div>}
    {error && <div className="triage-review__error"><XCircle size={17}/>{error}</div>}
    {success && <div className="triage-review__success"><CheckCircle2 size={17}/>{success}</div>}
    <div className="triage-review__layout">
      <section className="glass-card triage-review__queue">
        <div className="triage-review__section-title"><FileClock size={18}/><h3>Pending review</h3><span>{filteredWorkflows.length}{queueSearch.trim().isNotEmpty ? ` / ${workflows.length}` : ''}</span></div>
        <label className="triage-review__search"><Search size={15}/><input value={queueSearch} onChange={event => setQueueSearch(event.target.value)} placeholder="Search ID, urgency, or safety signal" aria-label="Search pending clinical reviews" />{queueSearch && <button type="button" onClick={() => setQueueSearch('')} aria-label="Clear search">×</button>}</label>
        {loading ? <p className="text-muted">Loading workflows…</p> : workflows.length === 0 ? <div className="triage-review__empty"><CheckCircle2 size={30}/><p>No workflows are awaiting clinical review.</p></div> : filteredWorkflows.length === 0 ? <div className="triage-review__empty"><Search size={28}/><p>No pending workflows match this search.</p></div> : filteredWorkflows.map(workflow => <button key={workflow.workflowId} className={`triage-review__queue-item ${selected?.workflowId === workflow.workflowId ? 'triage-review__queue-item--active' : ''}`} onClick={() => selectWorkflow(workflow)}><strong>Workflow #{workflow.workflowId}</strong><span>{workflow.triageLevel} · {workflow.uncertaintyState}</span><small>{workflow.redFlags?.join(', ') || workflow.riskFactors?.join(', ') || 'Clinical review required'}</small></button>)}
      </section>
      <section className="glass-card triage-review__details">
        {!selected ? <div className="triage-review__empty"><ClipboardCheck size={34}/><p>Select a workflow to review its safety summary.</p></div> : <>
          <div className="triage-review__section-title"><AlertTriangle size={18}/><h3>Workflow #{selected.workflowId}</h3><span className="triage-review__level">{humanize(selected.triageLevel)}</span></div>
          <div className="triage-review__review-required"><AlertTriangle size={18}/><div><strong>Clinical decision required</strong><span>This workflow remains in the queue until you approve, reject, or request more information.</span></div></div>
          <section className="triage-review__privacy-note"><ShieldCheck size={15}/><span><strong>Minimum necessary information:</strong> this view shows symptom information needed for clinical review. Contact details and account data are redacted.</span></section>
          <Info title="Patient’s submitted report" items={selected.patientReportedSymptoms ? [selected.patientReportedSymptoms] : []} fallback="The original patient report was not recorded." />
          <p className="triage-review__message">{selected.patientMessage}</p>
          <Info title="Emergency red flags" items={selected.redFlags} fallback="No configured emergency red flag was detected." />
          <Info title="Urgent warning signs" items={selected.urgentFlags} fallback="No configured urgent warning sign was detected." />
          <Info title="Serious or high-risk context" items={selected.clinicalReviewFlags} fallback="No separate serious/high-risk context was detected." />
          <ClinicalFacts facts={selected.clinicalFacts} />
          <Info title="Decision basis" items={selected.decisionBasis} fallback="No decision basis was recorded." />
          <Info title="Information limitations" items={selected.missingInformation} fallback="No limitations were recorded." />
          <details className="triage-review__audit"><summary><span>Technical audit trail</span><small>Optional · no raw patient text</small></summary><p>Use this only when you need to verify the workflow controls, tool use, retries, or safe-failure handling.</p>{auditEvents.map(event => <div key={`${event.stage}-${event.createdAt}`}><strong>{event.stage}</strong><span>{event.eventType}</span>{event.tool && <small>Tool: {event.tool}</small>}{event.outcome && <small>{event.outcome}</small>}{event.validationPassed !== null && event.validationPassed !== undefined && <small>Validation: {event.validationPassed ? 'passed' : 'failed'}</small>}{event.retryCount > 0 && <small>Retries: {event.retryCount}</small>}{event.errorCode && <small className="triage-review__trace-error">Safe failure: {event.errorCode}</small>}<time>{event.durationMs !== null && event.durationMs !== undefined ? `${event.durationMs} ms · ` : ''}{new Date(event.createdAt).toLocaleString()}</time></div>)}</details>
          {canMakeClinicalDecision ? <section className="triage-review__decision"><h4>Record your clinical decision</h4><p>Choose the action that reflects your independent assessment. A confirmation panel will appear before anything is saved.</p><div className="triage-review__actions"><Button variant="primary" icon={CheckCircle2} loading={acting} onClick={() => setPendingDecision('Approved')}>Approve recommendation</Button><Button variant="danger" icon={XCircle} disabled={acting} onClick={() => setPendingDecision('Rejected')}>Reject recommendation</Button><Button variant="secondary" icon={ClipboardCheck} disabled={acting} onClick={() => setPendingDecision('RevisionRequested')}>Request more information</Button></div></section> : <section className="triage-review__decision"><h4>Clinical decision restricted</h4><p>Administrators can inspect this workflow and its audit trail, but only a doctor can approve, reject, or request a revision.</p></section>}
        </>}
      </section>
    </div>
    {pendingDecision && <ReviewDecisionDialog decision={pendingDecision} note={reviewNote} acting={acting} onNoteChange={setReviewNote} onCancel={() => { setPendingDecision(null); setReviewNote('') }} onConfirm={() => review(pendingDecision)} />}
  </div>
}

function Info({ title, items, fallback }) { return <section className="triage-review__info"><h4>{title}</h4>{items?.length ? <ul>{items.map(item => <li key={item}>{item}</li>)}</ul> : <p>{fallback}</p>}</section> }

function ClinicalFacts({ facts }) {
  if (!facts) return <Info title="Grounded clinical facts" fallback="No structured facts were available." />
  const values = [
    facts.primaryConcept && `Primary symptom concept: ${facts.primaryConcept}`,
    facts.currentlyActive !== null && facts.currentlyActive !== undefined && `Currently active: ${facts.currentlyActive ? 'yes' : 'no'}`,
    facts.durationMinutes !== null && facts.durationMinutes !== undefined && `Duration: ${facts.durationMinutes} minutes`,
    facts.durationDays !== null && facts.durationDays !== undefined && `Duration: ${facts.durationDays} days`,
    facts.severityScore !== null && facts.severityScore !== undefined && `Reported severity: ${facts.severityScore}/10`,
    facts.temperatureCelsius !== null && facts.temperatureCelsius !== undefined && `Reported temperature: ${facts.temperatureCelsius}°C`,
    facts.progression && `Progression: ${facts.progression}`,
    ...(facts.warningSigns || []).map(value => `Present warning sign: ${value}`),
    ...(facts.negatedWarningSigns || []).map(value => `Explicitly denied warning sign: ${value}`),
    ...(facts.riskContexts || []).map(value => `Risk context: ${value}`),
    ...(facts.evidence || []).map(value => `Evidence for ${value.field}${value.value ? ` (${value.value})` : ''}: “${value.quote}”`),
  ].filter(Boolean)
  return <Info title="Grounded clinical facts" items={values} fallback="No structured facts were extracted." />
}

function ReviewDecisionDialog({ decision, note, acting, onNoteChange, onCancel, onConfirm }) {
  const content = decisionCopy(decision)
  const Icon = content.icon
  return <div className="triage-review__modal-backdrop" role="presentation"><section className="triage-review__modal" role="dialog" aria-modal="true" aria-labelledby="review-decision-title"><div className="triage-review__modal-icon"><Icon size={24}/></div><h2 id="review-decision-title">{content.title}</h2><p>{content.description}</p><label htmlFor="clinical-review-note">Clinical note <span>(optional)</span></label><textarea id="clinical-review-note" value={note} maxLength="1000" onChange={event => onNoteChange(event.target.value)} placeholder={content.placeholder} /><div className="triage-review__modal-actions"><Button variant="secondary" disabled={acting} onClick={onCancel}>Cancel</Button><Button variant={content.variant} icon={Icon} loading={acting} onClick={onConfirm}>{content.confirm}</Button></div></section></div>
}

function decisionCopy(decision) {
  if (decision === 'Approved') return { title: 'Approve SafeTriage recommendation?', description: 'You confirm that the proposed triage level is appropriate after your independent clinical review. This completes the review.', confirm: 'Confirm approval', placeholder: 'Reasoning or follow-up plan, if useful', variant: 'primary', icon: CheckCircle2 }
  if (decision === 'Rejected') return { title: 'Reject SafeTriage recommendation?', description: 'You do not accept the proposed triage recommendation. Record a note so the care team understands the alternative direction.', confirm: 'Confirm rejection', placeholder: 'Why the recommendation is not accepted or what should happen next', variant: 'danger', icon: XCircle }
  return { title: 'Request a revision?', description: 'The recommendation is neither approved nor rejected. Record what must be clarified or corrected; the workflow remains visible in the clinical-review queue.', confirm: 'Request revision', placeholder: 'What needs clarification, correction, or additional evidence?', variant: 'secondary', icon: ClipboardCheck }
}

function decisionLabel(decision) { return decisionCopy(decision).title.replace('?', '') }
function humanize(value) { return value?.replace(/([a-z])([A-Z])/g, '$1 $2') || 'Unknown' }
