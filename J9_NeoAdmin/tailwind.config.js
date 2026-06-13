/** @type {import('tailwindcss').Config} */
export default {
  content: [
    './Components/**/*.{razor,html,cs}',
    './wwwroot/**/*.html',
    // ProjectReference 开发时扫描框架内 Tailwind 类名（登录页 min-h-[26rem]、LayoutEmpty min-h-dvh 等）
    '../../NeoAdminProject/NeoAdmin.Blazor/**/*.{razor,html,cs}',
    // NuGet 发布包回退（Docker / 未挂源码时）
    '../.nuget/packages/neoadmin.blazor/**/lib/**/NeoAdmin.Blazor.dll',
  ],
};
