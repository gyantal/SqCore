// TypeScript 6 (TS2882) requires a module declaration for CSS side-effect imports used by webpack.
// TypeScript 6 (TS2882) rejects CSS side-effect imports such as import './../css/main.css' unless a module declaration exists. That broke all 9 webpack production apps.
declare module '*.css';
