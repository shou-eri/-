#!/usr/bin/env python3
"""
デバッグツールキット (Debug Toolkit)
コードのデバッグと機能追加のためのユーティリティ

Author: Research Development Team
Purpose: コードのデバッグ、機能の追加をサポートするツール
"""

import sys
import traceback
import time
import functools
from typing import Any, Callable, Dict, List, Optional
import logging

# ログ設定
logging.basicConfig(
    level=logging.DEBUG,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler('debug.log'),
        logging.StreamHandler(sys.stdout)
    ]
)

logger = logging.getLogger(__name__)


class DebugToolkit:
    """デバッグとコード分析のためのメインクラス"""
    
    def __init__(self):
        self.debug_mode = True
        self.performance_data = {}
        self.error_count = 0
        
    def debug_print(self, message: str, level: str = "INFO") -> None:
        """デバッグメッセージの出力"""
        if self.debug_mode:
            timestamp = time.strftime("%Y-%m-%d %H:%M:%S")
            print(f"[{level}] {timestamp}: {message}")
            logger.info(f"{level}: {message}")
    
    def trace_function(self, func: Callable) -> Callable:
        """関数の実行をトレースするデコレータ"""
        @functools.wraps(func)
        def wrapper(*args, **kwargs):
            func_name = func.__name__
            self.debug_print(f"関数 '{func_name}' を実行開始", "TRACE")
            self.debug_print(f"引数: args={args}, kwargs={kwargs}", "TRACE")
            
            start_time = time.time()
            try:
                result = func(*args, **kwargs)
                end_time = time.time()
                execution_time = end_time - start_time
                
                self.performance_data[func_name] = execution_time
                self.debug_print(f"関数 '{func_name}' 実行完了 (実行時間: {execution_time:.4f}秒)", "TRACE")
                self.debug_print(f"戻り値: {result}", "TRACE")
                
                return result
            except Exception as e:
                self.error_count += 1
                self.debug_print(f"関数 '{func_name}' でエラー発生: {str(e)}", "ERROR")
                self.debug_print(f"トレースバック:\n{traceback.format_exc()}", "ERROR")
                raise
        
        return wrapper
    
    def analyze_performance(self) -> Dict[str, float]:
        """パフォーマンス分析結果を返す"""
        if not self.performance_data:
            self.debug_print("パフォーマンスデータがありません", "WARNING")
            return {}
        
        self.debug_print("=== パフォーマンス分析 ===", "INFO")
        for func_name, exec_time in self.performance_data.items():
            self.debug_print(f"関数 '{func_name}': {exec_time:.4f}秒", "INFO")
        
        return self.performance_data.copy()
    
    def code_profiler(self, code_block: Callable) -> Dict[str, Any]:
        """コードブロックのプロファイリング"""
        import cProfile
        import pstats
        import io
        
        pr = cProfile.Profile()
        pr.enable()
        
        try:
            result = code_block()
            pr.disable()
            
            s = io.StringIO()
            ps = pstats.Stats(pr, stream=s).sort_stats('cumulative')
            ps.print_stats()
            
            profile_data = {
                'result': result,
                'profile_output': s.getvalue(),
                'success': True
            }
            
            self.debug_print("コードプロファイリング完了", "INFO")
            return profile_data
            
        except Exception as e:
            pr.disable()
            self.debug_print(f"プロファイリング中にエラー: {str(e)}", "ERROR")
            return {
                'result': None,
                'profile_output': str(e),
                'success': False
            }


class CodeAnalyzer:
    """コード分析とバグ検出のためのクラス"""
    
    def __init__(self, debug_toolkit: DebugToolkit):
        self.debug_toolkit = debug_toolkit
        self.issues_found = []
    
    def check_common_issues(self, code_str: str) -> List[Dict[str, str]]:
        """一般的なコードの問題をチェック"""
        issues = []
        lines = code_str.split('\n')
        
        for i, line in enumerate(lines, 1):
            # 基本的なチェック
            if 'print(' in line and not '#' in line:
                issues.append({
                    'line': i,
                    'type': 'デバッグprint文',
                    'description': 'print文が残っている可能性があります',
                    'severity': 'low'
                })
            
            if 'TODO' in line or 'FIXME' in line:
                issues.append({
                    'line': i,
                    'type': 'TODO/FIXME',
                    'description': '未完了のタスクがあります',
                    'severity': 'medium'
                })
            
            if 'except:' in line or 'except Exception:' in line:
                issues.append({
                    'line': i,
                    'type': '広範囲例外処理',
                    'description': '具体的な例外型を指定することを推奨',
                    'severity': 'medium'
                })
        
        self.issues_found.extend(issues)
        
        if issues:
            self.debug_toolkit.debug_print(f"{len(issues)}個の潜在的な問題を発見", "WARNING")
            for issue in issues:
                self.debug_toolkit.debug_print(
                    f"行{issue['line']}: {issue['type']} - {issue['description']}", 
                    "WARNING"
                )
        else:
            self.debug_toolkit.debug_print("問題は発見されませんでした", "INFO")
        
        return issues


def create_sample_buggy_code():
    """デバッグ練習用のバグがあるサンプルコード"""
    
    # バグ1: ゼロ除算エラー
    def divide_numbers(a, b):
        return a / b  # bが0の場合エラー
    
    # バグ2: リストのインデックス範囲外エラー
    def get_item_from_list(items, index):
        return items[index]  # インデックスチェックなし
    
    # バグ3: 型エラー
    def add_numbers(a, b):
        return a + b  # 型チェックなし
    
    # バグ4: 無限ループの可能性
    def count_down(n):
        while n > 0:
            print(n)
            # n -= 1 がコメントアウトされている
    
    return {
        'divide_numbers': divide_numbers,
        'get_item_from_list': get_item_from_list,
        'add_numbers': add_numbers,
        'count_down': count_down
    }


# 使用例とテスト関数
def demonstration():
    """デバッグツールキットのデモンストレーション"""
    print("=== デバッグツールキット デモンストレーション ===")
    
    # ツールキットの初期化
    toolkit = DebugToolkit()
    analyzer = CodeAnalyzer(toolkit)
    
    # 関数トレースのテスト
    @toolkit.trace_function
    def sample_function(x, y):
        time.sleep(0.1)  # 処理時間のシミュレーション
        return x * y + 10
    
    toolkit.debug_print("関数トレースのテスト開始", "INFO")
    result = sample_function(5, 3)
    toolkit.debug_print(f"結果: {result}", "INFO")
    
    # パフォーマンス分析
    toolkit.analyze_performance()
    
    # コード分析のテスト
    sample_code = '''
def example_function():
    print("デバッグ用のprint")  # この行は問題として検出される
    # TODO: この部分を実装する
    try:
        risky_operation()
    except:  # 広範囲な例外処理
        pass
'''
    
    toolkit.debug_print("コード分析テスト開始", "INFO")
    issues = analyzer.check_common_issues(sample_code)
    
    return toolkit, analyzer


if __name__ == "__main__":
    demonstration()