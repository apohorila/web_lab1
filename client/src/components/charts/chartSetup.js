import {
  Chart as ChartJS,
  ArcElement,
  BarElement,
  CategoryScale,
  LinearScale,
  LineElement,
  PointElement,
  Tooltip,
  Legend,
  Filler,
} from "chart.js";

// Реєструємо лише потрібні модулі Chart.js (так бандл менший)
ChartJS.register(
  ArcElement,
  BarElement,
  CategoryScale,
  LinearScale,
  LineElement,
  PointElement,
  Tooltip,
  Legend,
  Filler,
);

ChartJS.defaults.font.family = "'Halvar Breitschrift', system-ui, sans-serif";
ChartJS.defaults.color = "#525252";

// Монохромна палітра в стилі бренду
export const PALETTE = [
  "#111111",
  "#8a8a8a",
  "#c2a878",
  "#5f6b5a",
  "#a14d3b",
  "#3b5ba1",
  "#d9d4cc",
  "#7a5c3e",
];

export const colorAt = (i) => PALETTE[i % PALETTE.length];

export const money = (n) => `₴${Number(n || 0).toLocaleString("uk-UA")}`;
