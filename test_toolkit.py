#!/usr/bin/env python3
"""
簡単なテストスイート - デバッグ・機能追加ツールキット

Author: Research Development Team
Purpose: ツールキットの基本機能をテスト
"""

import unittest
import sys
import os
import time
from io import StringIO

# プロジェクトのルートディレクトリをパスに追加
sys.path.append(os.path.dirname(os.path.abspath(__file__)))

from debug_toolkit import DebugToolkit, CodeAnalyzer
from feature_enhancer import FeatureEnhancer


class TestDebugToolkit(unittest.TestCase):
    """DebugToolkitのテスト"""
    
    def setUp(self):
        """テストセットアップ"""
        self.toolkit = DebugToolkit()
        self.toolkit.debug_mode = True
    
    def test_debug_print(self):
        """デバッグメッセージ出力のテスト"""
        # 標準出力をキャプチャ
        captured_output = StringIO()
        sys.stdout = captured_output
        
        self.toolkit.debug_print("テストメッセージ", "INFO")
        
        # 標準出力を復元
        sys.stdout = sys.__stdout__
        
        output = captured_output.getvalue()
        self.assertIn("テストメッセージ", output)
        self.assertIn("INFO", output)
    
    def test_trace_function(self):
        """関数トレースのテスト"""
        @self.toolkit.trace_function
        def test_function(x, y):
            return x + y
        
        result = test_function(3, 4)
        self.assertEqual(result, 7)
        
        # パフォーマンスデータが記録されているかチェック
        self.assertIn('test_function', self.toolkit.performance_data)
    
    def test_performance_analysis(self):
        """パフォーマンス分析のテスト"""
        @self.toolkit.trace_function
        def slow_function():
            time.sleep(0.1)
            return "done"
        
        slow_function()
        
        perf_data = self.toolkit.analyze_performance()
        self.assertIn('slow_function', perf_data)
        self.assertGreater(perf_data['slow_function'], 0.05)  # 少なくとも50ms


class TestCodeAnalyzer(unittest.TestCase):
    """CodeAnalyzerのテスト"""
    
    def setUp(self):
        """テストセットアップ"""
        self.toolkit = DebugToolkit()
        self.analyzer = CodeAnalyzer(self.toolkit)
    
    def test_check_common_issues(self):
        """一般的な問題検出のテスト"""
        buggy_code = '''
print("デバッグ用")
# TODO: 実装する
try:
    risky_operation()
except:
    pass
'''
        
        issues = self.analyzer.check_common_issues(buggy_code)
        
        # 期待される問題が検出されているかチェック
        issue_types = [issue['type'] for issue in issues]
        self.assertIn('デバッグprint文', issue_types)
        self.assertIn('TODO/FIXME', issue_types)
        self.assertIn('広範囲例外処理', issue_types)


class TestFeatureEnhancer(unittest.TestCase):
    """FeatureEnhancerのテスト"""
    
    def setUp(self):
        """テストセットアップ"""
        self.toolkit = DebugToolkit()
        self.enhancer = FeatureEnhancer(self.toolkit)
    
    def test_add_logging_to_function(self):
        """ログ機能追加のテスト"""
        def simple_function(x):
            return x * 2
        
        logged_function = self.enhancer.add_logging_to_function(simple_function)
        result = logged_function(5)
        
        self.assertEqual(result, 10)
        self.assertIn("ログ機能を関数 'simple_function' に追加", self.enhancer.enhancement_history)
    
    def test_add_caching_to_function(self):
        """キャッシュ機能追加のテスト"""
        call_count = [0]  # リストを使ってミュータブルな変数にする
        
        def expensive_function(x):
            call_count[0] += 1
            return x ** 2
        
        cached_function = self.enhancer.add_caching_to_function(expensive_function)
        
        # 初回実行
        result1 = cached_function(5)
        self.assertEqual(result1, 25)
        self.assertEqual(call_count[0], 1)
        
        # 同じ引数で再実行（キャッシュから取得）
        result2 = cached_function(5)
        self.assertEqual(result2, 25)
        self.assertEqual(call_count[0], 1)  # 関数は再実行されていない
    
    def test_add_input_validation(self):
        """入力値検証追加のテスト"""
        def divide_function(a, b):
            return a / b
        
        validation_rules = {
            'a': {'type': (int, float)},
            'b': {'type': (int, float), 'min_value': 1}
        }
        
        validated_function = self.enhancer.add_input_validation(divide_function, validation_rules)
        
        # 正常なケース
        result = validated_function(10, 2)
        self.assertEqual(result, 5.0)
        
        # 型エラーのケース
        with self.assertRaises(TypeError):
            validated_function("10", 2)
        
        # 値エラーのケース
        with self.assertRaises(ValueError):
            validated_function(10, 0)
    
    def test_create_api_wrapper(self):
        """API形式レスポンス作成のテスト"""
        def simple_function(x, y):
            return x + y
        
        api_function = self.enhancer.create_api_wrapper(simple_function)
        response = api_function(3, 4)
        
        self.assertIsInstance(response, dict)
        self.assertIn('success', response)
        self.assertIn('data', response)
        self.assertTrue(response['success'])
        self.assertEqual(response['data'], 7)
    
    def test_enhancement_history(self):
        """機能追加履歴のテスト"""
        def dummy_function():
            pass
        
        self.enhancer.add_logging_to_function(dummy_function)
        self.enhancer.add_caching_to_function(dummy_function)
        
        history = self.enhancer.get_enhancement_history()
        self.assertEqual(len(history), 2)
        self.assertIn("ログ機能", history[0])
        self.assertIn("キャッシュ機能", history[1])


class TestIntegration(unittest.TestCase):
    """統合テスト"""
    
    def test_toolkit_integration(self):
        """ツールキットの統合テスト"""
        toolkit = DebugToolkit()
        enhancer = FeatureEnhancer(toolkit)
        
        @toolkit.trace_function
        def calculator(a, b, operation):
            if operation == 'add':
                return a + b
            elif operation == 'multiply':
                return a * b
            else:
                raise ValueError("未サポートの操作")
        
        # 機能追加
        logged_calculator = enhancer.add_logging_to_function(calculator)
        cached_calculator = enhancer.add_caching_to_function(logged_calculator)
        
        # テスト実行
        result1 = cached_calculator(5, 3, 'add')
        result2 = cached_calculator(5, 3, 'multiply')
        
        self.assertEqual(result1, 8)
        self.assertEqual(result2, 15)
        
        # パフォーマンスデータとエンハンスメント履歴の確認
        perf_data = toolkit.analyze_performance()
        history = enhancer.get_enhancement_history()
        
        self.assertGreater(len(perf_data), 0)
        self.assertGreater(len(history), 0)


def run_tests():
    """テストの実行"""
    print("=== デバッグ・機能追加ツールキット テスト実行 ===")
    
    # テストスイートの作成
    test_suite = unittest.TestSuite()
    
    # テストクラスを追加
    test_classes = [
        TestDebugToolkit,
        TestCodeAnalyzer,
        TestFeatureEnhancer,
        TestIntegration
    ]
    
    for test_class in test_classes:
        tests = unittest.TestLoader().loadTestsFromTestCase(test_class)
        test_suite.addTests(tests)
    
    # テスト実行
    runner = unittest.TextTestRunner(verbosity=2)
    result = runner.run(test_suite)
    
    # 結果のサマリー
    print(f"\n=== テスト結果サマリー ===")
    print(f"実行テスト数: {result.testsRun}")
    print(f"失敗: {len(result.failures)}")
    print(f"エラー: {len(result.errors)}")
    
    if result.failures:
        print("\n失敗したテスト:")
        for test, traceback in result.failures:
            print(f"- {test}")
    
    if result.errors:
        print("\nエラーが発生したテスト:")
        for test, traceback in result.errors:
            print(f"- {test}")
    
    if result.wasSuccessful():
        print("\n✅ すべてのテストが成功しました！")
    else:
        print("\n❌ 一部のテストが失敗しました。")
    
    return result.wasSuccessful()


if __name__ == "__main__":
    success = run_tests()
    sys.exit(0 if success else 1)