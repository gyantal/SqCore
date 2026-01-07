const path = require('path');
const HtmlWebpackPlugin = require('html-webpack-plugin');
const MiniCssExtractPlugin = require('mini-css-extract-plugin');
const HtmlInlineScriptPlugin = require('html-inline-script-webpack-plugin');
const HtmlInlineCssWebpackPlugin = require('html-inline-css-webpack-plugin').default;

module.exports = {
  entry: './webapps/LiveStrategy/UberTaa/ts/main.ts',
  output: {
    path: path.resolve(__dirname, './../../../wwwroot/webapps/LiveStrategy/UberTaa'),
    filename: '[name].[chunkhash].js',
    publicPath: '/webapps/LiveStrategy/UberTaa/',
    clean: true
  },
  resolve: {
    extensions: ['.js', '.ts']
  },
  module: {
    rules: [
      {
        test: /\.ts$/,
        use: 'ts-loader'
      },
      {
        test: /\.css$/,
        use: [MiniCssExtractPlugin.loader, 'css-loader']
      },
      {
        test: /\.(png|webp|jpe?g|gif|avif)$/i,
        type: 'asset/resource',
        generator: {
          filename: 'images/[contenthash][ext][query]'
        }
      }
    ]
  },
  plugins: [
    new MiniCssExtractPlugin({
      filename: './css/[name].[chunkhash].css'
    }),
    new HtmlWebpackPlugin({
      template: 'webapps/LiveStrategy/UberTaa/index.html'
    }),
    new HtmlInlineCssWebpackPlugin(),
    new HtmlInlineScriptPlugin()
  ]
};