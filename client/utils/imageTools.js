

export const ALLOWED_TYPES = ["image/jpeg", "image/png", "image/webp"];
export const MAX_INPUT_BYTES = 10 * 1024 * 1024; 
export const MAX_SIDE = 800; 
export const QUALITY = 0.75;


export function validateImageFile(file) {
  if (!file) return "Оберіть файл";
  if (!ALLOWED_TYPES.includes(file.type)) return "Дозволені лише зображення JPEG, PNG або WebP";
  if (file.size > MAX_INPUT_BYTES) return "Файл завеликий (максимум 10 МБ)";
  return null;
}

/**

 * @returns {Promise<{ blob: Blob, width: number, height: number }>}
 */
export async function compressImage(file, { maxSide = MAX_SIDE, quality = QUALITY } = {}) {
  const bitmap = await createImageBitmap(file);
  try {
    const scale = Math.min(1, maxSide / Math.max(bitmap.width, bitmap.height));
    const width = Math.round(bitmap.width * scale);
    const height = Math.round(bitmap.height * scale);

    const canvas = document.createElement("canvas");
    canvas.width = width;
    canvas.height = height;
    const context = canvas.getContext("2d");
    context.fillStyle = "#fff"; 
    context.fillRect(0, 0, width, height);
    context.drawImage(bitmap, 0, 0, width, height);

    const blob = await new Promise((resolve, reject) =>
      canvas.toBlob((result) => (result ? resolve(result) : reject(new Error("Не вдалося стиснути зображення"))), "image/jpeg", quality),
    );

    if (blob.size >= file.size) {
      return { blob: file, width: bitmap.width, height: bitmap.height };
    }
    return { blob, width, height };
  } finally {
    bitmap.close();
  }
}


export function blobToDataUrl(blob) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result);
    reader.onerror = () => reject(new Error("Не вдалося прочитати файл"));
    reader.readAsDataURL(blob);
  });
}

export function formatBytes(bytes) {
  if (bytes < 1024) return `${bytes} Б`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} КБ`;
  return `${(bytes / 1024 / 1024).toFixed(1)} МБ`;
}
