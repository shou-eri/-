#!/usr/bin/env python3
"""
サンプルコード集 - デバッグ練習用
バグを含んだコードサンプルと修正例

Author: Research Development Team
Purpose: デバッグスキル向上のための練習用コード
"""

from debug_toolkit import DebugToolkit
from feature_enhancer import FeatureEnhancer
import time


class BuggyCalculator:
    """バグを含んだ計算機クラス（デバッグ練習用）"""
    
    def __init__(self):
        self.history = []
        self.precision = 2
    
    def add(self, a, b):
        """加算 - バグ: 型チェックなし"""
        result = a + b
        self.history.append(f"{a} + {b} = {result}")
        return result
    
    def divide(self, a, b):
        """除算 - バグ: ゼロ除算チェックなし"""
        result = a / b  # ゼロ除算エラーの可能性
        self.history.append(f"{a} / {b} = {result}")
        return result
    
    def get_history_item(self, index):
        """履歴取得 - バグ: インデックス範囲外チェックなし"""
        return self.history[index]  # インデックスエラーの可能性
    
    def calculate_average(self, numbers):
        """平均値計算 - バグ: 空リストチェックなし"""
        total = sum(numbers)
        count = len(numbers)
        return total / count  # 空リストの場合ゼロ除算
    
    def factorial(self, n):
        """階乗計算 - バグ: 負数チェックなし、非効率"""
        if n == 0:
            return 1
        else:
            return n * self.factorial(n - 1)  # 負数で無限再帰の可能性


class DataProcessor:
    """データ処理クラス - 様々なバグを含む"""
    
    def __init__(self):
        self.data = []
        self.processed_count = 0
    
    def load_data_from_file(self, filename):
        """ファイルからデータ読み込み - バグ: ファイル存在チェックなし"""
        with open(filename, 'r') as f:  # FileNotFoundError の可能性
            lines = f.readlines()
            self.data = [line.strip() for line in lines]
        return len(self.data)
    
    def process_numbers(self, text_numbers):
        """数値処理 - バグ: 型変換エラーチェックなし"""
        results = []
        for text_num in text_numbers:
            num = int(text_num)  # ValueError の可能性
            results.append(num * 2)
        return results
    
    def find_max_value(self, values):
        """最大値検索 - バグ: 空リストチェックなし"""
        max_val = values[0]  # IndexError の可能性
        for val in values:
            if val > max_val:
                max_val = val
        return max_val
    
    def safe_divide_list(self, numerators, denominators):
        """リスト要素の除算 - バグ: リスト長不一致チェックなし"""
        results = []
        for i in range(len(numerators)):
            result = numerators[i] / denominators[i]  # IndexError の可能性
            results.append(result)
        return results


class NetworkManager:
    """ネットワーク管理クラス - エラーハンドリングのバグ"""
    
    def __init__(self):
        self.connections = {}
        self.timeout = 30
    
    def connect_to_server(self, server_url, port):
        """サーバー接続 - バグ: 例外処理が不適切"""
        try:
            # 実際の接続処理をシミュレート
            time.sleep(0.1)
            if "invalid" in server_url:
                raise ConnectionError("無効なサーバーURL")
            
            connection_id = f"{server_url}:{port}"
            self.connections[connection_id] = True
            return connection_id
            
        except:  # バグ: 広範囲すぎる例外処理
            print("接続エラーが発生しました")
            return None
    
    def send_data(self, connection_id, data):
        """データ送信 - バグ: 接続確認なし"""
        # バグ: connection_id の存在確認なし
        if self.connections[connection_id]:  # KeyError の可能性
            print(f"データ送信: {data}")
            return True
        return False
    
    def disconnect(self, connection_id):
        """接続切断 - バグ: メモリリーク"""
        if connection_id in self.connections:
            del self.connections[connection_id]
        # バグ: 関連リソースの解放が不完全


def demonstrate_buggy_code():
    """バグを含んだコードのデモンストレーション"""
    print("=== バグを含んだコード例 ===")
    
    toolkit = DebugToolkit()
    
    # 1. 計算機のバグ例
    print("\n1. バグのある計算機:")
    calc = BuggyCalculator()
    
    try:
        result1 = calc.add(10, 5)
        print(f"加算結果: {result1}")
        
        result2 = calc.add("Hello", " World")  # 型エラーが起きるはず
        print(f"文字列加算: {result2}")
        
        # ゼロ除算エラー
        result3 = calc.divide(10, 0)  # ZeroDivisionError
        
    except Exception as e:
        toolkit.debug_print(f"計算機エラー: {str(e)}", "ERROR")
    
    # 2. データ処理のバグ例
    print("\n2. データ処理のバグ:")
    processor = DataProcessor()
    
    try:
        # 存在しないファイル
        processor.load_data_from_file("nonexistent.txt")
        
    except Exception as e:
        toolkit.debug_print(f"ファイル読み込みエラー: {str(e)}", "ERROR")
    
    try:
        # 型変換エラー
        bad_numbers = ["1", "2", "abc", "4"]
        results = processor.process_numbers(bad_numbers)
        
    except Exception as e:
        toolkit.debug_print(f"数値処理エラー: {str(e)}", "ERROR")
    
    # 3. ネットワーク管理のバグ例
    print("\n3. ネットワーク管理のバグ:")
    network = NetworkManager()
    
    try:
        # 無効なサーバーURLで接続
        conn_id = network.connect_to_server("invalid_server", 8080)
        
        if conn_id:
            network.send_data(conn_id, "test data")
        else:
            # 存在しない接続IDで送信試行
            network.send_data("fake_connection", "test data")
            
    except Exception as e:
        toolkit.debug_print(f"ネットワークエラー: {str(e)}", "ERROR")


def demonstrate_fixed_code():
    """修正されたコードの例"""
    print("\n=== 修正されたコード例 ===")
    
    toolkit = DebugToolkit()
    enhancer = FeatureEnhancer(toolkit)
    
    # 修正された計算機
    class FixedCalculator:
        def __init__(self):
            self.history = []
            self.precision = 2
        
        def add(self, a, b):
            """型チェック付き加算"""
            if not isinstance(a, (int, float)) or not isinstance(b, (int, float)):
                raise TypeError("引数は数値である必要があります")
            
            result = a + b
            self.history.append(f"{a} + {b} = {result}")
            return result
        
        def divide(self, a, b):
            """ゼロ除算チェック付き除算"""
            if not isinstance(a, (int, float)) or not isinstance(b, (int, float)):
                raise TypeError("引数は数値である必要があります")
            
            if b == 0:
                raise ZeroDivisionError("ゼロで除算はできません")
            
            result = a / b
            self.history.append(f"{a} / {b} = {result}")
            return result
        
        def get_history_item(self, index):
            """範囲チェック付き履歴取得"""
            if not isinstance(index, int):
                raise TypeError("インデックスは整数である必要があります")
            
            if index < 0 or index >= len(self.history):
                raise IndexError("履歴インデックスが範囲外です")
            
            return self.history[index]
    
    # 修正されたコードのテスト
    print("修正された計算機のテスト:")
    fixed_calc = FixedCalculator()
    
    try:
        result1 = fixed_calc.add(10, 5)
        print(f"加算結果: {result1}")
        
        result2 = fixed_calc.divide(10, 2)
        print(f"除算結果: {result2}")
        
        # エラーハンドリングのテスト
        try:
            fixed_calc.divide(10, 0)
        except ZeroDivisionError as e:
            print(f"適切にキャッチされたエラー: {e}")
        
        history_item = fixed_calc.get_history_item(0)
        print(f"履歴項目: {history_item}")
        
    except Exception as e:
        toolkit.debug_print(f"予期しないエラー: {str(e)}", "ERROR")


if __name__ == "__main__":
    demonstrate_buggy_code()
    demonstrate_fixed_code()