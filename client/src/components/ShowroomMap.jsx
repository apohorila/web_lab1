import React, { useEffect } from "react";
import { MapContainer, TileLayer, Marker, Popup, useMap } from "react-leaflet";
import L from "leaflet";
import "leaflet/dist/leaflet.css";

const customIcon = new L.Icon({
  iconUrl: "https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png",
  iconRetinaUrl:
    "https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png",
  shadowUrl: "https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png",
  iconSize: [25, 41],
  iconAnchor: [12, 41],
  popupAnchor: [1, -34],
  shadowSize: [41, 41],
});

const getCityFromAddress = (address = "") => {
  if (!address) return "Інше";
  const clean = address.replace(/^(м\.|смт\.|село|м\s)/i, "").trim();
  const city = clean.split(",")[0].trim();
  return city || "Інше";
};

function MapController({ selectedShowroom, showrooms }) {
  const map = useMap();

  useEffect(() => {
    if (selectedShowroom?.latitude && selectedShowroom?.longitude) {
      map.flyTo([selectedShowroom.latitude, selectedShowroom.longitude], 15, {
        duration: 1.5,
      });
    } else if (showrooms.length > 0) {
      const bounds = L.latLngBounds(
        showrooms.map((s) => [s.latitude, s.longitude]),
      );
      map.fitBounds(bounds, { padding: [50, 50], maxZoom: 13 });
    }
  }, [selectedShowroom, showrooms, map]);

  return null;
}

export default function ShowroomMap({
  showrooms,
  selectedShowroom,
  onSelectShowroom,
}) {
  const defaultCenter = [49.0384, 31.4513];

  return (
    <div className="w-full h-[550px] border border-neutral-200 relative overflow-hidden z-0">
      <MapContainer
        center={defaultCenter}
        zoom={6}
        scrollWheelZoom={true}
        className="w-full h-full"
      >
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />

        <MapController
          selectedShowroom={selectedShowroom}
          showrooms={showrooms}
        />

        {showrooms.map((showroom) => (
          <Marker
            key={showroom.id}
            position={[showroom.latitude, showroom.longitude]}
            icon={customIcon}
            eventHandlers={{
              click: () => onSelectShowroom(showroom),
            }}
          >
            <Popup className="font-[Halvar_Breitschrift]">
              <div className="p-1">
                <span className="text-[10px] tracking-widest uppercase text-neutral-400 block mb-0.5">
                  {getCityFromAddress(showroom.address)}
                </span>
                <h3 className="text-sm font-medium tracking-wide uppercase text-neutral-900 mb-1">
                  {showroom.name || `Шоурум Bagelle`}
                </h3>
                <p className="text-xs text-neutral-600 mb-2">
                  {showroom.address}
                </p>
                {showroom.workingHours && (
                  <p className="text-[11px] text-neutral-500 mb-2">
                    Години: {showroom.workingHours}
                  </p>
                )}
                <a
                  href={`https://www.google.com/maps/search/?api=1&query=${showroom.latitude},${showroom.longitude}`}
                  target="_blank"
                  rel="noreferrer"
                  className="inline-block text-[10px] tracking-widest uppercase underline text-black hover:opacity-70"
                >
                  Прокласти маршрут ↗
                </a>
              </div>
            </Popup>
          </Marker>
        ))}
      </MapContainer>
    </div>
  );
}
