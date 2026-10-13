import React from "react";
import { Doughnut } from "react-chartjs-2";
import { colorAt } from "./chartSetup";

// Крихітна діаграма без легенди: розбивка кількості за категоріями
export default function MiniDoughnut({ items, size = 110 }) {
  const data = {
    labels: items.map((i) => i.name),
    datasets: [
      {
        data: items.map((i) => i.count),
        backgroundColor: items.map((_, idx) => colorAt(idx)),
        borderColor: "#fff",
        borderWidth: 2,
      },
    ],
  };

  const options = {
    responsive: false,
    animation: false,
    cutout: "62%",
    plugins: { legend: { display: false } },
  };

  return <Doughnut data={data} options={options} width={size} height={size} />;
}
