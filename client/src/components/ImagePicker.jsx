import { useEffect, useState } from "react";
import { blobToDataUrl, compressImage, formatBytes, validateImageFile } from "../utils/imageTools";


export default function ImagePicker({ onPicked }) {
  const [preview, setPreview] = useState(null); // { src, info }
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  
  useEffect(() => {
    return () => {
      if (preview?.src) URL.revokeObjectURL(preview.src);
    };
  }, [preview]);

  const handleFile = async (event) => {
    const file = event.target.files?.[0];
    event.target.value = ""; 
    if (!file) return;

    setError("");
    const problem = validateImageFile(file);
    if (problem) {
      setError(problem);
      return;
    }

    try {
      setBusy(true);
      const { blob, width, height } = await compressImage(file);
      setPreview({
        src: URL.createObjectURL(blob),
        info: `${file.name}: ${formatBytes(file.size)} → ${formatBytes(blob.size)} (${width}×${height})`,
      });
      onPicked(await blobToDataUrl(blob));
    } catch {
      setError("Не вдалося обробити фото: файл пошкоджений або не є зображенням");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="mt-[8px] font-[Halvar_Breitschrift]">
      <label className="inline-block px-[14px] py-[8px] text-[11px] tracking-widest uppercase border border-neutral-300 bg-[white] hover:border-black cursor-pointer">
        {busy ? "Обробка..." : "Обрати фото з комп'ютера"}
        <input type="file" accept="image/jpeg,image/png,image/webp" onChange={handleFile} disabled={busy} className="hidden" />
      </label>

      {error && <p className="mt-[6px] text-[11px] text-red-600">{error}</p>}

      {preview && (
        <div className="mt-[8px] flex items-center gap-[12px]">
          <img src={preview.src} alt="Попередній перегляд" className="w-[72px] h-[72px] object-cover border border-neutral-200" />
          <p className="m-0 text-[11px] text-neutral-500">{preview.info}</p>
        </div>
      )}
    </div>
  );
}
