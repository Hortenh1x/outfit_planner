import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { TagChipsEditor } from './TagChipsEditor';

afterEach(cleanup);

function setup(tags: string[], existingTags: string[] = []) {
  const onChange = vi.fn();
  render(
    <TagChipsEditor tags={tags} existingTags={existingTags} ariaLabel="Tags for shirt" onChange={onChange} />
  );
  return { onChange };
}

describe('TagChipsEditor', () => {
  it('renders a chip per tag', () => {
    setup(['linen', 'summer']);

    const editor = screen.getByLabelText('Tags for shirt');
    expect(within(editor).getByText('linen')).toBeInTheDocument();
    expect(within(editor).getByText('summer')).toBeInTheDocument();
  });

  it('removes a tag with the trash button', async () => {
    const { onChange } = setup(['linen', 'summer']);

    await userEvent.click(screen.getByRole('button', { name: /remove tag linen/i }));

    expect(onChange).toHaveBeenCalledWith(['summer']);
  });

  it('edits a tag inline on click and commits on Enter', async () => {
    const { onChange } = setup(['linen', 'summer']);

    await userEvent.click(screen.getByRole('button', { name: /edit tag linen/i }));
    const input = screen.getByRole('textbox', { name: /edit tag linen/i });
    await userEvent.clear(input);
    await userEvent.type(input, 'cotton{enter}');

    expect(onChange).toHaveBeenCalledWith(['cotton', 'summer']);
  });

  it('cancels an inline edit on Escape', async () => {
    const { onChange } = setup(['linen']);

    await userEvent.click(screen.getByRole('button', { name: /edit tag linen/i }));
    const input = screen.getByRole('textbox', { name: /edit tag linen/i });
    await userEvent.clear(input);
    await userEvent.type(input, 'cotton{escape}');

    expect(onChange).not.toHaveBeenCalled();
    expect(screen.getByText('linen')).toBeInTheDocument();
  });

  it('removes a tag when its inline edit is cleared and committed', async () => {
    const { onChange } = setup(['linen', 'summer']);

    await userEvent.click(screen.getByRole('button', { name: /edit tag linen/i }));
    const input = screen.getByRole('textbox', { name: /edit tag linen/i });
    await userEvent.clear(input);
    await userEvent.type(input, '{enter}');

    expect(onChange).toHaveBeenCalledWith(['summer']);
  });

  it('merges when an edit duplicates another tag', async () => {
    const { onChange } = setup(['linen', 'summer']);

    await userEvent.click(screen.getByRole('button', { name: /edit tag summer/i }));
    const input = screen.getByRole('textbox', { name: /edit tag summer/i });
    await userEvent.clear(input);
    await userEvent.type(input, 'linen{enter}');

    expect(onChange).toHaveBeenCalledWith(['linen']);
  });

  it('adds normalized tags from the add input, splitting commas and deduping', async () => {
    const { onChange } = setup(['linen']);

    const addInput = screen.getByLabelText('Add tag');
    await userEvent.type(addInput, 'Cotton, linen, wool{enter}');

    expect(onChange).toHaveBeenCalledWith(['linen', 'cotton', 'wool']);
  });

  it('does not render interactive controls when disabled', () => {
    const onChange = vi.fn();
    render(
      <TagChipsEditor tags={['linen']} ariaLabel="Tags for shirt" disabled onChange={onChange} />
    );

    expect(screen.getByText('linen')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /remove tag linen/i })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Add tag')).not.toBeInTheDocument();
  });
});
