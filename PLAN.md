このライブラリを使用する時、現状だとファイル名(相対パス)を指定するときは FilePathプロパティ, 
特定のフォルダに保存したい場合は `UseStandardSaveDirectory()`などを使用してフォルダを指定する構成となっている。
これを以下のように改修したい。

src/Configuration.Writable.Core/WritableOptionsConfigurationBuilder.csも参照。

* メソッドチェーンで繋げられるようにする。
* 複数の指定を可能にする(その場合、上位のパスが利用可能ならそこを、なければ下位のパスを利用する)

```csharp
// メソッド名は例なので適当に変更してください

// only FilePath
opt.RelativeFilePath("hoge/fuga");

// UseStandardSaveDirectory + FilePath
opt.UseStandardSaveDirectory("myapp")
   .RelativeFilePath("hoge/fuga");

// UseStandardSaveDirectory(if production) + RelativeFilePath
var isProduction = builder.Environment.IsProduction();
opt.UseStandardSaveDirectory("myapp", enabled: isProduction)
   .RelativeFilePath("hoge/fuga");

// support multiple locations
opt.MultipleSaveLocations(lb => {
    // first, use C:/Data/MyApp if available
    // (if this path is not existing/writable, next one will be used)
    lb.AbsoluteFilePath("C:/Data/MyApp/hoge/fuga");
    // second, use D:/Data/MyApp
    lb.AbsoluteFilePath("D:/Data/MyApp/hoge/fuga");
    // third, use standard location
    lb.UseStandardSaveDirectory("myapp").RelativeFilePath("hoge/fuga");
    // last, use relative to exe location
    lb.RelativeFilePath("hoge/fuga");
});

```