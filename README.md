# デバッグ・機能追加ツールキット (Debug & Feature Enhancement Toolkit)

> コードのデバッグ、機能の追加のための統合開発ツール
> 
> An integrated development toolkit for code debugging and feature enhancement

## 概要 (Overview)

このプロジェクトは、ソフトウェア開発における**コードのデバッグ**と**機能の追加**を効率的に行うためのツールキットです。研究開発やソフトウェアプロトタイピングにおいて、迅速なデバッグと機能拡張をサポートします。

This project provides a comprehensive toolkit for **code debugging** and **feature enhancement** in software development, particularly useful for research and rapid prototyping.

## 主な機能 (Key Features)

### 🔍 デバッグツール (Debug Tools)
- **関数トレース**: 関数の実行状況をリアルタイムで監視
- **パフォーマンス分析**: 実行時間の測定と分析
- **エラー追跡**: 詳細なエラーログとトレースバック
- **コード分析**: 一般的なバグパターンの自動検出

### ⚡ 機能追加ツール (Feature Enhancement Tools)
- **ログ機能追加**: 既存関数へのログ機能の自動追加
- **キャッシュ機能**: 計算結果のキャッシング機能
- **リトライ機能**: 失敗時の自動再試行機能
- **入力値検証**: 型チェックと値範囲の検証
- **API化**: 関数をAPI形式のレスポンスに変換

### 📚 学習用サンプル (Learning Samples)
- **バグのあるコード例**: 一般的なバグパターンのサンプル
- **修正されたコード例**: 適切なエラーハンドリングの実装例
- **ベストプラクティス**: コーディング標準に準拠した例

## ファイル構成 (File Structure)

```
├── README.md                 # このファイル
├── main.py                   # メインスクリプト（統合デモ）
├── debug_toolkit.py          # デバッグ機能のメインモジュール
├── feature_enhancer.py       # 機能追加モジュール
├── sample_codes.py           # サンプルコードとデモンストレーション
├── test_toolkit.py           # テストスイート
├── debug.log                 # デバッグログファイル（実行時生成）
└── enhancement_log.json      # 機能追加履歴（実行時生成）
```

## インストールと実行 (Installation & Usage)

### 1. 基本的な実行方法

```bash
# メインスクリプトの実行
python main.py

# 個別モジュールの実行
python debug_toolkit.py
python feature_enhancer.py
python sample_codes.py
```

### 2. テストの実行

```bash
# 全テストの実行
python test_toolkit.py

# 特定のテストクラスのみ実行
python -m unittest test_toolkit.TestDebugToolkit
```

## 使用例 (Usage Examples)

### デバッグツールの使用

```python
from debug_toolkit import DebugToolkit, CodeAnalyzer

# ツールキットの初期化
toolkit = DebugToolkit()
analyzer = CodeAnalyzer(toolkit)

# 関数トレースの追加
@toolkit.trace_function
def my_function(x, y):
    return x * y + 10

# 実行とパフォーマンス分析
result = my_function(5, 3)
toolkit.analyze_performance()

# コード分析
code = '''
def example():
    print("debug output")  # 問題として検出される
    # TODO: 実装が必要
'''
issues = analyzer.check_common_issues(code)
```

### 機能追加ツールの使用

```python
from feature_enhancer import FeatureEnhancer

enhancer = FeatureEnhancer()

# 既存関数に機能を追加
def original_function(a, b):
    return a + b

# ログ機能の追加
logged_func = enhancer.add_logging_to_function(original_function)

# キャッシュ機能の追加
cached_func = enhancer.add_caching_to_function(original_function)

# 入力値検証の追加
validation_rules = {
    'a': {'type': int, 'min_value': 0},
    'b': {'type': int, 'min_value': 0}
}
validated_func = enhancer.add_input_validation(original_function, validation_rules)

# API形式レスポンスの作成
api_func = enhancer.create_api_wrapper(original_function)
response = api_func(5, 3)  # {'success': True, 'data': 8, ...}
```

## メニュー機能 (Menu Features)

メインスクリプト実行時に表示される対話型メニュー:

1. **デバッグツールのデモ** - 基本的なデバッグ機能の紹介
2. **機能追加ツールのデモ** - 機能拡張の実演
3. **バグのあるコード例** - 一般的なバグパターンの学習
4. **修正されたコード例** - 適切な実装の確認
5. **コード分析機能** - 自動的な問題検出
6. **総合デモンストレーション** - 全機能の統合使用例
7. **ヘルプ・使い方** - 詳細な使用方法

## ログ機能 (Logging)

- **debug.log**: 全てのデバッグメッセージが記録されます
- **enhancement_log.json**: 機能追加の履歴が保存されます
- **コンソール出力**: リアルタイムでの情報表示

## 対象用途 (Target Applications)

- 🧪 **研究開発**: プロトタイプの迅速なデバッグ
- 📖 **教育**: プログラミング学習とデバッグ技術の習得
- 🔧 **開発支援**: 既存コードの機能拡張
- 🐛 **バグハンティング**: 一般的な問題の早期発見
- 📊 **コード品質**: パフォーマンス分析と改善

## 技術仕様 (Technical Specifications)

- **言語**: Python 3.6+
- **依存関係**: Python標準ライブラリのみ
- **ライセンス**: MIT License
- **文字エンコーディング**: UTF-8
- **ログレベル**: DEBUG, INFO, WARNING, ERROR

## 貢献 (Contributing)

このプロジェクトへの貢献を歓迎します：

1. バグ報告
2. 機能提案
3. コードの改善
4. ドキュメントの充実
5. テストケースの追加

## 作者 (Author)

Research Development Team  
Email: seagoniokst77@gmail.com

---

**研究・開発・学習のためのコードデバッグと機能追加を効率化するツールキット**

*A toolkit to streamline code debugging and feature enhancement for research, development, and learning purposes.*
