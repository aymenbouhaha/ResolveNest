import { CircleCheck, TriangleAlert } from 'lucide-react'
import type { ReactNode } from 'react'
import type { PresentedRelevanceNote } from '../lib/relevanceNoteText'

interface RelevanceNotesProps {
  supporting: PresentedRelevanceNote[]
  limitations: PresentedRelevanceNote[]
}

export function RelevanceNotes({ supporting, limitations }: RelevanceNotesProps) {
  return (
    <div className="grid gap-4 md:grid-cols-2">
      <NoteList
        title="Why this may be related"
        notes={supporting}
        emptyText="Nothing beyond the shared product area."
        icon={<CircleCheck className="mt-0.5 size-4 shrink-0 text-evidence-confirmed" aria-hidden="true" />}
      />
      <NoteList
        title="Caveats"
        notes={limitations}
        emptyText="No caveats were identified."
        icon={<TriangleAlert className="mt-0.5 size-4 shrink-0 text-evidence-candidate" aria-hidden="true" />}
      />
    </div>
  )
}

interface NoteListProps {
  title: string
  notes: PresentedRelevanceNote[]
  emptyText: string
  icon: ReactNode
}

function NoteList({ title, notes, emptyText, icon }: NoteListProps) {
  return (
    <div className="space-y-2">
      <h4 className="text-xs font-medium tracking-wide text-muted-foreground uppercase">{title}</h4>
      {notes.length > 0 ? (
        <ul className="space-y-2">
          {notes.map((note) => (
            <li key={note.code} className="flex gap-2 text-sm">
              {icon}
              <span>{note.text}</span>
            </li>
          ))}
        </ul>
      ) : (
        <p className="text-sm text-muted-foreground">{emptyText}</p>
      )}
    </div>
  )
}
