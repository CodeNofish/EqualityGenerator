using System;

namespace EqualityGenerator;

/// <summary>
/// 字符串处理辅助方法。
/// </summary>
public static class StringHelpers {
  // 支持的三种换行符
  private static readonly string[] _newLineChars = ["\r\n", "\r", "\n"];

  /// <summary>
  /// 按换行符拆成多行，忽略空行。
  /// </summary>
  public static string[] ToLines(this string text) {
    return text.Split(_newLineChars, StringSplitOptions.RemoveEmptyEntries);
  }
}