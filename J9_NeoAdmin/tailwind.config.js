/** @type {import('tailwindcss').Config} */
export default {
  content: [
    './Components/**/*.{razor,html,cs}',
    './wwwroot/**/*.html',
    '../.nuget/packages/neoadmin.blazor/**/lib/**/NeoAdmin.Blazor.dll',
  ],
};
