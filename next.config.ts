import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  reactStrictMode: true,
  poweredByHeader: false,
  // Genera un servidor autocontenido para el instalador de Windows.
  output: "standalone",
  // La base de datos vive en disco, fuera del bundle.
  serverExternalPackages: [],
};

export default nextConfig;
