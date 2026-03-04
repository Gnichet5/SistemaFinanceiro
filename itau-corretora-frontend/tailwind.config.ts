// tailwind.config.ts
import type { Config } from "tailwindcss";

const config: Config = {
  content: [
    "./src/pages/**/*.{js,ts,jsx,tsx,mdx}",
    "./src/components/**/*.{js,ts,jsx,tsx,mdx}",
    "./src/app/**/*.{js,ts,jsx,tsx,mdx}",
  ],
  theme: {
    extend: {
      colors: {
        itau: {
          orange: "#EC7000",
          blue: "#002B5E",
          bg: "#F4F5F7",
        },
      },
    },
  },
  plugins: [],
};
export default config;