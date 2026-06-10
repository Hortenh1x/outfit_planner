import { ImagePlus } from 'lucide-react';

export function FilePicker({
  label,
  fileName,
  onChange
}: {
  label: string;
  fileName?: string;
  onChange: (file: File | null) => void;
}) {
  return (
    <label className="file-picker">
      <span>{label}</span>
      <span className="file-picker-control">
        <span className="file-picker-action">
          <ImagePlus size={18} />
          Choose photo
        </span>
        <span className={fileName ? 'file-picker-name selected' : 'file-picker-name'}>
          {fileName ?? 'No file selected'}
        </span>
      </span>
      <input
        type="file"
        accept="image/png,image/jpeg,image/webp"
        onChange={(event) => onChange(event.target.files?.[0] ?? null)}
        required
      />
    </label>
  );
}
