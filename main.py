#!/usr/bin/env python3
"""
メインスクリプト - デバッグ・機能追加ツールキットの統合デモ

Author: Research Development Team
Purpose: コードのデバッグ、機能の追加のための統合ツールキット
"""

import sys
import time
from debug_toolkit import DebugToolkit, CodeAnalyzer, demonstration as debug_demo
from feature_enhancer import FeatureEnhancer, demonstrate_feature_enhancement
from sample_codes import demonstrate_buggy_code, demonstrate_fixed_code


def main():
    """メイン実行関数"""
    print("=" * 60)
    print("コードデバッグ・機能追加ツールキット")
    print("Debug & Feature Enhancement Toolkit")
    print("=" * 60)
    
    # ツールキットの初期化
    toolkit = DebugToolkit()
    enhancer = FeatureEnhancer(toolkit)
    analyzer = CodeAnalyzer(toolkit)
    
    toolkit.debug_print("ツールキット初期化完了", "INFO")
    
    # メニュー表示
    while True:
        print("\n" + "=" * 40)
        print("選択してください:")
        print("1. デバッグツールのデモ")
        print("2. 機能追加ツールのデモ")
        print("3. バグのあるコード例")
        print("4. 修正されたコード例")
        print("5. コード分析機能")
        print("6. 総合デモンストレーション")
        print("7. ヘルプ・使い方")
        print("0. 終了")
        print("=" * 40)
        
        try:
            choice = input("選択 (0-7): ").strip()
            
            if choice == "0":
                print("ツールキットを終了します。")
                toolkit.debug_print("ツールキット終了", "INFO")
                break
            
            elif choice == "1":
                print("\n--- デバッグツールのデモ ---")
                debug_demo()
            
            elif choice == "2":
                print("\n--- 機能追加ツールのデモ ---")
                demonstrate_feature_enhancement()
            
            elif choice == "3":
                print("\n--- バグのあるコード例 ---")
                demonstrate_buggy_code()
            
            elif choice == "4":
                print("\n--- 修正されたコード例 ---")
                demonstrate_fixed_code()
            
            elif choice == "5":
                print("\n--- コード分析機能 ---")
                demonstrate_code_analysis(analyzer)
            
            elif choice == "6":
                print("\n--- 総合デモンストレーション ---")
                comprehensive_demo(toolkit, enhancer, analyzer)
            
            elif choice == "7":
                print("\n--- ヘルプ・使い方 ---")
                show_help()
            
            else:
                print("無効な選択です。0-7の数字を入力してください。")
                
        except KeyboardInterrupt:
            print("\n\nCtrl+Cが押されました。ツールキットを終了します。")
            break
        except Exception as e:
            toolkit.debug_print(f"予期しないエラー: {str(e)}", "ERROR")
            print("エラーが発生しました。続行します...")


def demonstrate_code_analysis(analyzer):
    """コード分析のデモンストレーション"""
    
    # サンプルコードの分析
    sample_codes = {
        "問題のあるコード1": '''
def process_data(data):
    print("処理開始")  # デバッグprint
    result = []
    for i in range(len(data)):
        if data[i] > 100:  # マジックナンバー
            result.append(data[i] * 2)
    # TODO: エラーハンドリングを追加
    return result
''',
        
        "問題のあるコード2": '''
def connect_database():
    try:
        connection = create_connection()
        return connection
    except:  # 広範囲例外処理
        print("データベース接続エラー")
        return None

def very_long_function_name_that_exceeds_eighty_characters_and_should_be_refactored():
    pass
''',
        
        "良いコード例": '''
def calculate_tax(amount, tax_rate=0.1):
    """税額を計算する"""
    if not isinstance(amount, (int, float)):
        raise TypeError("金額は数値である必要があります")
    
    if amount < 0:
        raise ValueError("金額は負の値にできません")
    
    return amount * tax_rate
'''
    }
    
    for code_name, code in sample_codes.items():
        print(f"\n--- {code_name} の分析 ---")
        print("コード:")
        print(code)
        
        issues = analyzer.check_common_issues(code)
        
        if issues:
            print(f"\n発見された問題 ({len(issues)}件):")
            for issue in issues:
                print(f"  行{issue['line']}: {issue['type']} - {issue['description']}")
        else:
            print("\n問題は発見されませんでした。")


def comprehensive_demo(toolkit, enhancer, analyzer):
    """総合デモンストレーション"""
    print("総合的なツールキットの使用例を示します...")
    
    # 1. 問題のある関数を定義
    def problematic_function(x, y):
        """問題のある関数の例"""
        time.sleep(0.1)  # 処理時間のシミュレーション
        return x / y  # ゼロ除算の可能性
    
    print("\n1. 元の問題のある関数:")
    try:
        result = problematic_function(10, 2)
        print(f"正常な場合の結果: {result}")
        
        # 問題のあるケース
        result = problematic_function(10, 0)
    except ZeroDivisionError as e:
        toolkit.debug_print(f"ゼロ除算エラー: {str(e)}", "ERROR")
    
    # 2. 機能追加によって改善
    print("\n2. 機能追加による改善:")
    
    # ログ機能を追加
    logged_function = enhancer.add_logging_to_function(problematic_function)
    
    # リトライ機能を追加
    retry_function = enhancer.add_retry_mechanism(logged_function, max_retries=2)
    
    # 入力値検証を追加
    validation_rules = {
        'x': {'type': (int, float)},
        'y': {'type': (int, float), 'min_value': 0.001}  # ゼロに近い値を制限
    }
    validated_function = enhancer.add_input_validation(retry_function, validation_rules)
    
    # 改善された関数のテスト
    try:
        result = validated_function(10, 2)
        print(f"改善された関数の結果: {result}")
        
        # 問題のあるケースをテスト
        result = validated_function(10, 0)
    except (ValueError, ZeroDivisionError) as e:
        toolkit.debug_print(f"適切に処理されたエラー: {str(e)}", "INFO")
    
    # 3. コード分析
    print("\n3. コード分析結果:")
    function_code = '''
def problematic_function(x, y):
    time.sleep(0.1)  # TODO: 最適化が必要
    print("デバッグ用出力")
    return x / y  # ゼロ除算チェックなし
'''
    
    issues = analyzer.check_common_issues(function_code)
    
    # 4. パフォーマンス分析
    print("\n4. パフォーマンス分析:")
    toolkit.analyze_performance()
    
    # 5. 機能追加履歴
    print("\n5. 機能追加履歴:")
    for enhancement in enhancer.get_enhancement_history():
        print(f"  - {enhancement}")
    
    print("\n総合デモンストレーション完了!")


def show_help():
    """ヘルプとツールの使い方を表示"""
    help_text = """
=== デバッグ・機能追加ツールキット ヘルプ ===

このツールキットは以下の機能を提供します:

【デバッグツール (debug_toolkit.py)】
- DebugToolkit: デバッグメッセージ出力、関数トレース、パフォーマンス分析
- CodeAnalyzer: コードの問題点検出、一般的なバグパターンの発見

【機能追加ツール (feature_enhancer.py)】
- FeatureEnhancer: 既存コードへの機能追加
  * ログ機能追加
  * キャッシュ機能追加
  * リトライ機能追加
  * 入力値検証追加
  * API形式レスポンス変換

【サンプルコード (sample_codes.py)】
- BuggyCalculator: バグを含んだ計算機クラス
- DataProcessor: データ処理の一般的なバグ例
- NetworkManager: ネットワーク処理のエラーハンドリング例

【使用方法】
1. ツールキットをインポート:
   from debug_toolkit import DebugToolkit
   from feature_enhancer import FeatureEnhancer

2. 基本的な使用例:
   toolkit = DebugToolkit()
   enhancer = FeatureEnhancer(toolkit)
   
   @toolkit.trace_function
   def my_function():
       pass

3. 機能追加例:
   enhanced_func = enhancer.add_logging_to_function(original_func)

【ファイル構成】
- debug_toolkit.py: デバッグ機能
- feature_enhancer.py: 機能追加機能
- sample_codes.py: サンプルとデモコード
- main.py: メインスクリプト（このファイル）

【ログファイル】
- debug.log: デバッグメッセージのログ
- enhancement_log.json: 機能追加履歴

詳細な使用例は各メニューのデモンストレーションを参照してください。
"""
    print(help_text)


if __name__ == "__main__":
    try:
        main()
    except Exception as e:
        print(f"致命的エラー: {str(e)}")
        sys.exit(1)