import { useId, useRef, useState, type KeyboardEvent } from 'react';
import { Trash2 } from 'lucide-react';
import { normalizeTagToken, parseTokenText, uniqueTokens } from './wardrobeUpload';

interface TagChipsEditorProps {
  tags: string[];
  existingTags?: string[];
  disabled?: boolean;
  ariaLabel: string;
  onChange: (tags: string[]) => void;
}

/**
 * Editable oval tag chips. Click a chip to rename it inline (Enter/blur commit,
 * Escape cancel, empty removes, duplicate merges); the trash button removes it;
 * the trailing input adds tags (comma-separated, suggested from existing tags).
 * Any change emits a normalized, de-duplicated list through `onChange`.
 */
export function TagChipsEditor({ tags, existingTags = [], disabled = false, ariaLabel, onChange }: TagChipsEditorProps) {
  const [editingIndex, setEditingIndex] = useState<number | null>(null);
  const [editingValue, setEditingValue] = useState('');
  const [addValue, setAddValue] = useState('');
  const skipBlurRef = useRef(false);
  const datalistId = useId();

  const suggestions = existingTags
    .map(normalizeTagToken)
    .filter((tag) => tag.length > 0 && !tags.includes(tag));

  function emitIfChanged(next: string[]) {
    if (next.length !== tags.length || next.some((tag, index) => tag !== tags[index])) {
      onChange(next);
    }
  }

  function startEdit(index: number) {
    setEditingIndex(index);
    setEditingValue(tags[index]);
  }

  function commitEdit() {
    if (editingIndex === null) {
      return;
    }

    const value = normalizeTagToken(editingValue);
    const draft = [...tags];
    if (value) {
      draft[editingIndex] = value;
    } else {
      draft.splice(editingIndex, 1);
    }

    setEditingIndex(null);
    setEditingValue('');
    emitIfChanged(uniqueTokens(draft));
  }

  function cancelEdit() {
    skipBlurRef.current = true;
    setEditingIndex(null);
    setEditingValue('');
  }

  function handleEditKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Enter') {
      event.preventDefault();
      commitEdit();
    } else if (event.key === 'Escape') {
      event.preventDefault();
      cancelEdit();
    }
  }

  function handleEditBlur() {
    if (skipBlurRef.current) {
      skipBlurRef.current = false;
      return;
    }

    commitEdit();
  }

  function addTags(raw: string) {
    const additions = parseTokenText(raw);
    setAddValue('');
    if (additions.length === 0) {
      return;
    }

    emitIfChanged(uniqueTokens([...tags, ...additions]));
  }

  function handleAddKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Enter') {
      event.preventDefault();
      addTags(addValue);
    }
  }

  function removeTag(index: number) {
    emitIfChanged(tags.filter((_, position) => position !== index));
  }

  return (
    <div className="tag-chips-editor" aria-label={ariaLabel}>
      {tags.map((tag, index) => {
        if (disabled) {
          return (
            <span className="tag-chip" key={tag}>
              <span className="tag-chip-label">{tag}</span>
            </span>
          );
        }

        if (editingIndex === index) {
          return (
            <span className="tag-chip editing" key={`editing-${tag}`}>
              <input
                className="tag-chip-input"
                aria-label={`Edit tag ${tag}`}
                value={editingValue}
                autoFocus
                onChange={(event) => setEditingValue(event.target.value)}
                onKeyDown={handleEditKeyDown}
                onBlur={handleEditBlur}
              />
            </span>
          );
        }

        return (
          <span className="tag-chip" key={tag}>
            <button
              type="button"
              className="tag-chip-label"
              aria-label={`Edit tag ${tag}`}
              onClick={() => startEdit(index)}
            >
              {tag}
            </button>
            <button
              type="button"
              className="tag-chip-remove"
              aria-label={`Remove tag ${tag}`}
              onClick={() => removeTag(index)}
            >
              <Trash2 size={12} aria-hidden="true" />
            </button>
          </span>
        );
      })}
      {disabled ? null : (
        <>
          <input
            className="tag-chip-add"
            aria-label="Add tag"
            placeholder="+ tag"
            list={datalistId}
            value={addValue}
            onChange={(event) => setAddValue(event.target.value)}
            onKeyDown={handleAddKeyDown}
            onBlur={() => addTags(addValue)}
          />
          {suggestions.length > 0 ? (
            <datalist id={datalistId}>
              {suggestions.map((tag) => (
                <option value={tag} key={tag} />
              ))}
            </datalist>
          ) : null}
        </>
      )}
    </div>
  );
}
