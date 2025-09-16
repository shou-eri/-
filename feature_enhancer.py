#!/usr/bin/env python3
"""
機能追加ツールキット (Feature Enhancement Toolkit)
既存コードに新機能を追加するためのユーティリティ

Author: Research Development Team
Purpose: コードの機能拡張とリファクタリングをサポート
"""

import ast
import inspect
import json
import os
from typing import Any, Callable, Dict, List, Optional, Tuple
from debug_toolkit import DebugToolkit


class FeatureEnhancer:
    """機能追加とコード拡張のためのクラス"""
    
    def __init__(self, debug_toolkit: Optional[DebugToolkit] = None):
        self.debug_toolkit = debug_toolkit or DebugToolkit()
        self.enhancement_history = []
        
    def add_logging_to_function(self, func: Callable) -> Callable:
        """既存の関数にログ機能を追加"""
        import functools
        
        @functools.wraps(func)
        def wrapper(*args, **kwargs):
            func_name = func.__name__
            self.debug_toolkit.debug_print(f"[LOG] 関数 '{func_name}' 開始", "INFO")
            
            try:
                result = func(*args, **kwargs)
                self.debug_toolkit.debug_print(f"[LOG] 関数 '{func_name}' 正常終了", "INFO")
                return result
            except Exception as e:
                self.debug_toolkit.debug_print(f"[LOG] 関数 '{func_name}' エラー: {str(e)}", "ERROR")
                raise
        
        self.enhancement_history.append(f"ログ機能を関数 '{func.__name__}' に追加")
        return wrapper
    
    def add_caching_to_function(self, func: Callable) -> Callable:
        """既存の関数にキャッシュ機能を追加"""
        import functools
        
        cache = {}
        
        @functools.wraps(func)
        def wrapper(*args, **kwargs):
            # キャッシュキーの生成
            cache_key = str(args) + str(sorted(kwargs.items()))
            
            if cache_key in cache:
                self.debug_toolkit.debug_print(f"[CACHE] キャッシュヒット: {func.__name__}", "INFO")
                return cache[cache_key]
            
            result = func(*args, **kwargs)
            cache[cache_key] = result
            self.debug_toolkit.debug_print(f"[CACHE] 結果をキャッシュ: {func.__name__}", "INFO")
            
            return result
        
        self.enhancement_history.append(f"キャッシュ機能を関数 '{func.__name__}' に追加")
        return wrapper
    
    def add_retry_mechanism(self, func: Callable, max_retries: int = 3) -> Callable:
        """既存の関数にリトライ機能を追加"""
        import functools
        import time
        
        @functools.wraps(func)
        def wrapper(*args, **kwargs):
            for attempt in range(max_retries + 1):
                try:
                    return func(*args, **kwargs)
                except Exception as e:
                    if attempt == max_retries:
                        self.debug_toolkit.debug_print(
                            f"[RETRY] 関数 '{func.__name__}' 最終試行失敗: {str(e)}", "ERROR"
                        )
                        raise
                    else:
                        self.debug_toolkit.debug_print(
                            f"[RETRY] 関数 '{func.__name__}' 試行 {attempt + 1} 失敗、再試行中...", "WARNING"
                        )
                        time.sleep(1)  # 1秒待機
        
        self.enhancement_history.append(f"リトライ機能を関数 '{func.__name__}' に追加 (最大{max_retries}回)")
        return wrapper
    
    def add_input_validation(self, func: Callable, validation_rules: Dict[str, Any]) -> Callable:
        """既存の関数に入力値検証機能を追加"""
        import functools
        
        @functools.wraps(func)
        def wrapper(*args, **kwargs):
            func_signature = inspect.signature(func)
            bound_args = func_signature.bind(*args, **kwargs)
            bound_args.apply_defaults()
            
            # 検証ルールをチェック
            for param_name, value in bound_args.arguments.items():
                if param_name in validation_rules:
                    rule = validation_rules[param_name]
                    
                    if 'type' in rule and not isinstance(value, rule['type']):
                        type_name = rule['type'].__name__ if hasattr(rule['type'], '__name__') else str(rule['type'])
                        raise TypeError(f"引数 '{param_name}' は {type_name} 型である必要があります")
                    
                    if 'min_value' in rule and value < rule['min_value']:
                        raise ValueError(f"引数 '{param_name}' は {rule['min_value']} 以上である必要があります")
                    
                    if 'max_value' in rule and value > rule['max_value']:
                        raise ValueError(f"引数 '{param_name}' は {rule['max_value']} 以下である必要があります")
                    
                    if 'allowed_values' in rule and value not in rule['allowed_values']:
                        raise ValueError(f"引数 '{param_name}' は {rule['allowed_values']} のいずれかである必要があります")
            
            self.debug_toolkit.debug_print(f"[VALIDATION] 関数 '{func.__name__}' の入力値検証完了", "INFO")
            return func(*args, **kwargs)
        
        self.enhancement_history.append(f"入力値検証を関数 '{func.__name__}' に追加")
        return wrapper
    
    def create_api_wrapper(self, func: Callable) -> Callable:
        """関数をAPI風のレスポンス形式でラップ"""
        import functools
        import time
        
        @functools.wraps(func)
        def wrapper(*args, **kwargs):
            start_time = time.time()
            
            try:
                result = func(*args, **kwargs)
                end_time = time.time()
                
                response = {
                    'success': True,
                    'data': result,
                    'error': None,
                    'execution_time': end_time - start_time,
                    'timestamp': time.strftime('%Y-%m-%d %H:%M:%S')
                }
                
                self.debug_toolkit.debug_print(f"[API] 関数 '{func.__name__}' API応答生成完了", "INFO")
                return response
                
            except Exception as e:
                end_time = time.time()
                
                response = {
                    'success': False,
                    'data': None,
                    'error': str(e),
                    'execution_time': end_time - start_time,
                    'timestamp': time.strftime('%Y-%m-%d %H:%M:%S')
                }
                
                self.debug_toolkit.debug_print(f"[API] 関数 '{func.__name__}' エラー応答生成", "ERROR")
                return response
        
        self.enhancement_history.append(f"API形式のレスポンスを関数 '{func.__name__}' に追加")
        return wrapper
    
    def get_enhancement_history(self) -> List[str]:
        """これまでの機能追加履歴を取得"""
        return self.enhancement_history.copy()
    
    def save_enhancement_log(self, filepath: str = "enhancement_log.json") -> None:
        """機能追加履歴をファイルに保存"""
        log_data = {
            'timestamp': time.strftime('%Y-%m-%d %H:%M:%S'),
            'enhancements': self.enhancement_history
        }
        
        with open(filepath, 'w', encoding='utf-8') as f:
            json.dump(log_data, f, ensure_ascii=False, indent=2)
        
        self.debug_toolkit.debug_print(f"機能追加履歴を {filepath} に保存しました", "INFO")


class CodeRefactorer:
    """コードリファクタリングのためのクラス"""
    
    def __init__(self, debug_toolkit: Optional[DebugToolkit] = None):
        self.debug_toolkit = debug_toolkit or DebugToolkit()
    
    def extract_method(self, code_str: str, method_name: str, start_line: int, end_line: int) -> str:
        """指定された行範囲からメソッドを抽出"""
        lines = code_str.split('\n')
        extracted_lines = lines[start_line-1:end_line]
        
        # インデントを調整
        min_indent = min(len(line) - len(line.lstrip()) for line in extracted_lines if line.strip())
        adjusted_lines = [line[min_indent:] if len(line) >= min_indent else line for line in extracted_lines]
        
        method_code = f"def {method_name}():\n"
        for line in adjusted_lines:
            method_code += f"    {line}\n"
        
        self.debug_toolkit.debug_print(f"メソッド '{method_name}' を抽出しました", "INFO")
        return method_code
    
    def suggest_improvements(self, code_str: str) -> List[Dict[str, str]]:
        """コード改善提案を生成"""
        suggestions = []
        lines = code_str.split('\n')
        
        for i, line in enumerate(lines, 1):
            if len(line) > 80:
                suggestions.append({
                    'line': i,
                    'type': '行の長さ',
                    'suggestion': '行が80文字を超えています。分割を検討してください。'
                })
            
            if 'magic_number' in line or any(char.isdigit() for char in line if char not in ['0', '1']):
                if not any(keyword in line for keyword in ['range(', 'len(', 'enumerate(']):
                    suggestions.append({
                        'line': i,
                        'type': 'マジックナンバー',
                        'suggestion': '数値を定数として定義することを検討してください。'
                    })
        
        return suggestions


# サンプル使用例
def demonstrate_feature_enhancement():
    """機能追加デモンストレーション"""
    print("=== 機能追加ツールキット デモンストレーション ===")
    
    # ツールキットの初期化
    toolkit = DebugToolkit()
    enhancer = FeatureEnhancer(toolkit)
    
    # サンプル関数
    def simple_calculator(a, b, operation):
        if operation == 'add':
            return a + b
        elif operation == 'subtract':
            return a - b
        elif operation == 'multiply':
            return a * b
        elif operation == 'divide':
            return a / b
        else:
            raise ValueError("未サポートの操作です")
    
    # 機能追加のデモンストレーション
    print("\n1. ログ機能の追加:")
    logged_calculator = enhancer.add_logging_to_function(simple_calculator)
    result1 = logged_calculator(10, 5, 'add')
    print(f"結果: {result1}")
    
    print("\n2. キャッシュ機能の追加:")
    cached_calculator = enhancer.add_caching_to_function(simple_calculator)
    result2 = cached_calculator(10, 5, 'multiply')  # 初回実行
    result3 = cached_calculator(10, 5, 'multiply')  # キャッシュから取得
    
    print("\n3. 入力値検証の追加:")
    validation_rules = {
        'a': {'type': (int, float), 'min_value': 0},
        'b': {'type': (int, float), 'min_value': 0},
        'operation': {'allowed_values': ['add', 'subtract', 'multiply', 'divide']}
    }
    validated_calculator = enhancer.add_input_validation(simple_calculator, validation_rules)
    
    try:
        result4 = validated_calculator(10, 5, 'add')
        print(f"検証成功: {result4}")
    except (TypeError, ValueError) as e:
        print(f"検証エラー: {e}")
    
    print("\n4. API形式のレスポンス:")
    api_calculator = enhancer.create_api_wrapper(simple_calculator)
    api_result = api_calculator(10, 5, 'divide')
    print(f"API応答: {json.dumps(api_result, indent=2, ensure_ascii=False)}")
    
    # 機能追加履歴の表示
    print("\n=== 機能追加履歴 ===")
    for enhancement in enhancer.get_enhancement_history():
        print(f"- {enhancement}")
    
    return enhancer


if __name__ == "__main__":
    demonstrate_feature_enhancement()