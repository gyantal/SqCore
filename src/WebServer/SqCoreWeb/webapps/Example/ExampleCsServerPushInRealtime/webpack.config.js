const path = require("path");
const HtmlWebpackPlugin = require("html-webpack-plugin");
const { CleanWebpackPlugin } = require('clean-webpack-plugin');
const MiniCssExtractPlugin = require("mini-css-extract-plugin");
var HtmlWebpackInlineSourcePlugin = require('html-webpack-inline-source-plugin');

module.exports = {
    entry: "./webapps/Example/ExampleCsServerPushInRealtime/ts/main.ts",
    output: {
        path: path.resolve(__dirname, "./../../../wwwroot/webapps/Example/ExampleCsServerPushInRealtime"),
        filename: "[name].[chunkhash].js",
        publicPath: "/webapps/Example/ExampleCsServerPushInRealtime/"
    },
    resolve: {
        extensions: [".js", ".ts"]
    },
    module: {
        rules: [
            {
                test: /\.ts$/,
                use: "ts-loader"
            },
            {
                test: /\.css$/,
                use: [MiniCssExtractPlugin.loader, "css-loader"]
            },
            {
                test: /\.(png|jpe?g|gif)$/i,
                loader: 'file-loader',
                options: {
                    outputPath: 'images',
                }
            }
        ]
    },
    plugins: [
        new CleanWebpackPlugin({
            dry: false,     // default: false
            verbose: true,  // default: false
            cleanStaleWebpackAssets: true,  // default: true
            protectWebpackAssets: false,    // default: true
            cleanOnceBeforeBuildPatterns: ['**/*', '!static-files*']
        }),
        new HtmlWebpackPlugin({
            template: "webapps/Example/ExampleCsServerPushInRealtime/index.html",
            inlineSource: '.(js|css)$' // embed all javascript and css inline
        }),
        new HtmlWebpackInlineSourcePlugin(HtmlWebpackPlugin),
        new MiniCssExtractPlugin({
            filename: "./css/[name].[chunkhash].css"
        })
    ]
};