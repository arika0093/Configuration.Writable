# Configuration.Writable
現代C#における、ユーザー設定を簡単に扱うための軽量なライブラリ。

## なぜこのライブラリなのか？
C#アプリケーションにおいてユーザー設定を取り扱う方法は多岐にわたります。しかし、どれも何かしらの欠点があり、デファクトスタンダードたりうるものは存在していません。

### `app.config`(`Settings.settings`)を使う
古くからある方法で、検索すると多くの情報がヒットします(困ったことに……！)。使い始めると、おそらく以下のような問題に直面するでしょう。

* XMLベースの構成ファイルを手で記述する必要があります。(または、Visual Studioの使いにくいGUIを使う)
* 型安全性に欠け、複雑な設定には不向きです。
* 特に意識しないと頒布物にファイルが含まれるため、アップデート時に設定が初期化されるリスクがあります。

### 自前で設定ファイルを読み書きする
型安全性を確保することを考えると、自前で設定ファイルを用意し、それを読み書きする方法がまず思い浮かびます。
この方法は悪くないですが、あまりにも考えなければならないことが多いのが欠点です。

* 設定管理のためのコードを自分で書く必要があります。
* バックアップの作成、設定のマージ、アップデート対応など、多くの機能を自前で実装する必要があります。
* 複数の設定ソースを統合するのは一手間かかります。
* 設定変更の反映は自前で実装する必要があります。

### (任意の設定ライブラリ)を使う
あまりにも定型句が多いので、きっと何かしらの設定ライブラリが存在するはずです。
実際、NuGetを[`Config`で検索する](https://www.nuget.org/packages?q=config)だけでも多くのライブラリがヒットします。
この中の主要なものを見てみましたが、以下の理由で採用できませんでした。

* [DotNetConfig](https://github.com/dotnetconfig/dotnet-config)
  * ファイル形式は独自の形式(`.netconfig`)です。
  * 主に`dotnet tools`向けに設計されているようです。
* [Config.Net](https://github.com/aloneguid/config)
  * さまざまなプロバイダーをサポートしていますが、[独特の格納形式](https://github.com/aloneguid/config#flatline-syntax)を使用しています。
  * JSONプロバイダーにおいて、コレクションの書き込みが[サポートされていません](https://github.com/aloneguid/config#json)。

### `Microsoft.Extensions.Configuration`を使う
これらの現状を考えると、`Microsoft.Extensions.Configuration`(`MS.E.C`)が現代において最も標準化された設定管理の方法と言えます。
複数ファイルの統合・環境変数を含む様々なフォーマットのサポート・設定変更の反映など多くの機能が提供されており、`IHostApplication`ともシームレスに統合されます。
しかし、基本的にアプリケーション設定を想定しているため、ユーザー設定を扱うには不十分です。特に大きな問題として、設定の書き込みがサポートされていません。
またもう一つの問題として、DIを前提としているためコンソールアプリケーションなどで使うには少し手間がかかります。
設定ファイルを使いたいアプリはどちらかというとDIを使わないことが多いでしょう（例として `WinForms`, `WPF`, `コンソールアプリ`など）。

### `Configuration.Writable`
前置きが長くなりましたが、宣伝の時間です！
このライブラリは、`Microsoft.Extensions.Configuration`を拡張し、ユーザー設定の書き込みを簡単に行えるようにします。（読み込み部分は`MS.E.C`の機能をそのまま使っています）
また、DIを使わないアプリケーションでも簡単に使えるように設計されています。

## 使い方
### 準備
NuGetから`Configuration.Writable`をインストールします。

```shell
dotnet add package Configuration.Writable
```

そして、設定として読み書きしたいクラス(`SampleSetting`)を事前に準備します。

```csharp
public class SampleSetting
{
		public string Name { get; set; } = "default name";
		public int Age { get; set; } = 20;
}
```

### DIを使わない場合 (Console, WinForms, WPFなど)
`WritableConfig<SampleSetting>`を起点として、設定の読み書きを行います。

```csharp
using Configuration.Writable;

// 起動時に一度だけ初期化する
WritableConfig<SampleSetting>.Initialize();

// 設定を読み込む
var sampleSetting = WritableConfig<SampleSetting>.GetValue();
Console.WriteLine($">> Name: {sampleSetting.Name}");

// 設定を書き込む
sampleSetting.Name = "new name";
WritableConfig<SampleSetting>.Save(sampleSetting);
// 標準では ./usersettings.json に保存されます。
```

### DIを使う場合 (ASP.NET Core, Blazor, Worker Serviceなど)
まず、`builder.AddUserConfigurationFile`を呼び出し、設定を登録します。

```csharp
// Program.cs
builder.AddUserConfigurationFile<SampleSetting>();

// もしIHostApplicationを使用していない場合は、以下のようにします。
var configuration = new ConfigurationManager();
services.AddUserConfigurationFile<SampleSetting>(configuration);
```

その後、以下のようにDIコンテナから`IReadonlyConfig<T>`または`IWritableConfig<T>`を取得して使用します。

```csharp
// read config in your class
public class ConfigReadClass(IReadonlyConfig<SampleSetting> config)
{
		public void Print()
		{
				var sampleSetting = config.CurrentValue;
				Console.WriteLine($">> Name: {sampleSetting.Name}");
		}
}

// read and write config in your class
public class ConfigReadWriteClass(IWritableConfig<SampleSetting> config)
{
		public async Task UpdateAsync()
		{
				var sampleSetting = config.CurrentValue;
				sampleSetting.Name = "new name";
				await config.SaveAsync(sampleSetting);
		}
}
```

## カスタマイズ
### 設定方法
`WritableConfig<SampleSetting>.Initialize()`または`AddUserConfigurationFile<SampleSetting>()`の引数として、各種設定を変更することができます。

```csharp
// DIを使わない場合
WritableConfig<SampleSetting>.Initialize(options => {
		// 例
		options.FileName = "mysettings.json";
});

// DIを使う場合
builder.AddUserConfigurationFile<SampleSetting>(options => {
		// 例
		options.FileName = "mysettings.json";
});
```
### 主な設定項目
```csharp
{
		// 保存するファイル名 (デフォルト: "usersettings")
		// 例えば、親ディレクトリに保存したい場合は、"../usersettings"のように指定します。
		// 拡張子はプロバイダーによって自動的に付与されるため、指定不要です。
		options.FileName = "usersettings"; 

		// もし一般的な設定ディレクトリに保存したい場合、この関数を実行します。
		// 例えば、Windowsでは %APPDATA%/MyApp/ に保存されます。
		options.UseStandardSaveLocation("MyApp");

		// 設定ファイル保存のプロバイダー
		// デフォルトではJSON形式で保存します。
		// サポートしている一覧
		// JSON: WritableConfigJsonProvider<T>
		// XML:  WritableConfigXmlProvider<T>
		// YAML: WritableConfigYamlProvider<T> (Configuration.Writable.Yamlパッケージが必要)
		options.Provider = new WritableConfigJsonProvider<SampleSetting>();
}
```

