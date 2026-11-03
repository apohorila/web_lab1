import { useEffect, useState } from "react";
import {
  blobToDataUrl,
  compressImage,
  formatBytes,
  validateImageFile,
} from "../utils/imageTools";

export default function ImagePicker({ onPicked }) {
  const [preview, setPreview] = useState(null);
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

    let compressedBlob;
    let imgWidth;
    let imgHeight;

    try {
      setBusy(true);
      const { blob, width, height } = await compressImage(file);
      compressedBlob = blob;
      imgWidth = width;
      imgHeight = height;
      setPreview({
        src: URL.createObjectURL(blob),
        info: `${file.name}: ${formatBytes(file.size)} → ${formatBytes(blob.size)} (${width}×${height})`,
      });
    } catch {
      console.error("Помилка стиснення:", err);
      setError(
        "Не вдалося обробити фото: файл пошкоджений або не є зображенням",
      );
      setBusy(false);
      return;
    }
    try {
      const formData = new FormData();
      formData.append("file", compressedBlob, file.name);

      // Використовуємо ключі локального сховища з Bagelle
      const token =
        localStorage.getItem("bagelle_token") || localStorage.getItem("token");

      const response = await fetch("http://localhost:5038/api/media/upload", {
        method: "POST",
        headers: {
          Authorization: `Bearer ${token}`,
        },
        body: formData,
      });

      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(
          `Помилка сервера (${response.status}): ${errorText || "S3 Storage недоступний"}`,
        );
      }

      const data = await response.json();
      onPicked(data.url);
    } catch (err) {
      console.error("Помилка завантаження на сервер:", err);
      setError(err.message || "Помилка мережі при завантаженні у Storage");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="mt-[8px] font-[Halvar_Breitschrift]">
      <label className="inline-block px-[14px] py-[8px] text-[11px] tracking-widest uppercase border border-neutral-300 bg-[white] hover:border-black cursor-pointer">
        {busy ? "Обробка..." : "Обрати фото з комп'ютера"}
        <input
          type="file"
          accept="image/jpeg,image/png,image/webp"
          onChange={handleFile}
          disabled={busy}
          className="hidden"
        />
      </label>

      {error && <p className="mt-[6px] text-[11px] text-red-600">{error}</p>}

      {preview && (
        <div className="mt-[8px] flex items-center gap-[12px]">
          <img
            src={preview.src}
            alt="Попередній перегляд"
            className="w-[72px] h-[72px] object-cover border border-neutral-200"
          />
          <p className="m-0 text-[11px] text-neutral-500">{preview.info}</p>
        </div>
      )}
    </div>
  );
}
