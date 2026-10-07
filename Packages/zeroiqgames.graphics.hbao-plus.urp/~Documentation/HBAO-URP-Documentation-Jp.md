# HBAO+ URP ドキュメンテーション

## インストール方法
[こちら](../../../readme.md)を参照してください。

## 使い方
1. __Renderer Feature追加__
    * Universal Renderer Dataアセットを選択する
    * Add Renderer Featureボタンを押下する
    * 開かれるメニューから`Horizon-Based Ambient Occlusion`を選択する
2. __ボリューム追加__
   * シーンにボリュームコンポーネントを追加する
   * コンポーネントにボリュームプロフィールをアサインする
   * ボリュームプロフィールのインスペクターで`Add Override`ボタンを押下する
   * `Lighting -> HBAO+`を選択する
   * `Enabled`設定をオーバーライドして有効にする

## 見た目設定

### AOの設定
* **Enabled**
  * 有効かの設定。チェックを外すとHBAO+が無効になる
* **Intensity** (強さ)
  * エフェクトの全体的強さを指定する
* **Radius** (エフェクト半径)
  * 周りにAOをさせる最大距離
  * 単位：メートル
* **Direct Lighting Strength**
  * 光源に適用されるAO比例
  * １ではAmbient lightと普通の光源が同じAOを受ける
* **Background AO** (背景の最低AO)
  * 非常に遠いオブジェクトのAOピクセル半径が一定の値以下にならないようにする
  * `Background View Depth`より遠いものは`Background View Depth`と同じピクセル半径を使う
  * `Background View Depth`より遠いオブジェクトのAOが強くなる
* **Foreground AO** (前景の最大AO)
  * カメラに非常に近いオブジェクトのAOピクセル半径が一定以上にならないようにする
  * `Foreground View Depth`より近いものは`Foreground View Depth`と同じピクセル半径を使う
  * `Foreground View Depth`より近いオブジェクトのAOが弱くなる
* **Small Scale AO**
  * `Radius`の4分の１の距離以内にあるオブジェクトから受けるAOの係数
* **Large Scale AO**
  * `Radius`の4分の１の距離の外にあるオブジェクトから受けるAOの係数
* **Bias**
  * マイクロオクルージョンのartifactを直すための値
  * 大きく設定されるほどAOが発生するのに必要な最小限のオクルージョンが上がる

### ぼかしエフェクトの設定
* **Enabled**
  * 有効かの設定。チェックを外すとぼかしエフェクトが無効になる
* **Uniform Sharpness**
  * Bilateral filterで別オブジェクトと判定される最大の距離の差の係数
  * 大きいほど最大の距離の差が小さくなる
  * `Depth Dependent Sharpness`が無効な場合のみ使われる
  * 高ぼかしクオリティー以外では無視される
* **Depth Dependent Sharpness**
  * 適用される`Sharpness`をカメラからの距離で補間する
* **Foreground Sharpness**
  * `Foreground View Depth`より近いオブジェクトに適用される`Sharpness`の値
  * `Depth Dependent Sharpness`が有効な場合のみ使われる
  * `Foreground View Depth`と`Background View Depth`の間のオブジェクトの`Sharpness`が補間される
* **Background Sharpness**
  * `Background View Depth`より遠いオブジェクトに適用される`Sharpness`の値
  * `Depth Dependent Sharpness`が有効な場合のみ使われる
  * `Foreground View Depth`と`Background View Depth`の間のオブジェクトの`Sharpness`が補間される

## クオリティー設定
クオリティーに関する設定は[Renderer Feature](#使い方)で調整できる。

* **AO Quality** (オクルージョンのクオリティー)
   * 高(`Absolute Cinema`)、中(`Lowkey Good`)、低(`FPSmaxxing`)の３つの設定から選べる
   * 設定が高いほどrayの数と各ray上のステップ数が上がるが重くなる
   * 低設定ではartifactが発生することがある
* **Blur Quality** (ぼかしのクオリティー)
  * 高(`Absolute Cinema`)、中(`Lowkey Good`)、低(`FPSmaxxing`)の３つの設定から選べる
    * 高設定ではbilateral filterが使われる
      * 高設定を選択した場合は`Sharpness Source`という設定が有効になる
      * `Sharpness Source`は周りのピクセルのAOをぼかす時は指定したソースで同じオブジェクトだと判定した場合のみ混ぜる
      * `Depth`はカメラからの距離の差が一定の値以内の場合のみ混ぜる
      * `Normal`は表面の法線の差が一定の値以内の場合のみ混ぜる
      * `Both`は上記条件が両方満たされる場合のみ混ぜる
      * 重さの順番：Depth(一番軽い) -> Normal -> Both(一番重い)
    * 中設定はGaussian filterを使う
    * 低設定はKawase filterを使う
    * 中/低設定は周りのピクセルのDepthや法線に関係なく全部ブレンドされる
* **Geometry source** (シーン形状のソース)
  * AOの計算に必要なシーン形状のソース設定
  * `Depth Only`はカメラのdepth bufferのみを使って法線はdepth dataから再構築する
  * `Depth + Normal`はカメラのDepth bufferとnormalを両方使う
  * 事情によっては`Depth Only`の方が軽い場合もあるが`Depth + Normal`の方を推奨する
* **Normal Reconstruction Quality** (法線再構築のクオリティー)
  * 高(`Absolute Cinema`)、中(`Lowkey Good`)、低(`FPSmaxxing`)の３つの設定から選べる
  * `Geometry Source`を`Depth Only`に設定した場合のみ使われる。
* **Blur Radius** (ぼかしエフェクトの半径)
  * 単位：ピクセル
  * 周りの何ピクセルがぼかしエフェクトに使われるかを指定する
  * コストはピクセル数と線形的に上がる
  * ２Pixelはコスパが一番高い
* **Use Surface Slope For Depth Sharpness**
  * 有効にすると、HBAO+はぼかしのエッジ計算に表面の傾きを使う
* **Use Per Frame Random Jitter** (フレームごとにjitterをランダム化)
  * チェックを入れるとフレームごとにrayの方向が変わる
  * Temporalエフェクト（TAA等）を使う場合はbanding artifact等が減少する
  * Noiseが発生するので必要ない場合は無効にするのを推奨する
* **AO Render Path Preference Order**
  * AO計算のレンダーパスの優先度を指定する
  * デバイスに対応されて優先度が一番高いパスが使われる
  * 優先度が高く設定されていてもデバイスに対応されていないパスは使われない
  * ３つパス全部指定がないと設定が無視されてデフォルト優先度が使われる
* **Blur Preferred Render Path**
  * ぼかしエフェクトに使うレンダーパス
  * Automaticの際はパフォーマンスが一番高いと判定された設定が自動的に選ばれる

