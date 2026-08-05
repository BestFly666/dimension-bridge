using System;
using System.ComponentModel;

namespace SimpleXmlEditor.Services
{
    public enum XmlFormat
    {
        LocalisationData,
        ExcelSpreadsheet
    }

    public enum ReviewStatus
    {
        NotReviewed,
        Reviewed,
        NeedsFix
    }

    public class LocalizationEntry : INotifyPropertyChanged
    {
        private int _rowNumber;
        private string _key = "";
        private string _value = "";
        private string _translation = "";
        private bool _isSelected;
        private ReviewStatus _reviewStatus = ReviewStatus.NotReviewed;
        private double _evaluationScore = -1; // -1 表示未评估
        private string _evaluationImprovement = "";
        private bool _isBlacklisted;

        public int RowNumber
        {
            get => _rowNumber;
            set { _rowNumber = value; OnPropertyChanged(nameof(RowNumber)); }
        }

        public string Key
        {
            get => _key;
            set { _key = value; OnPropertyChanged(nameof(Key)); }
        }

        public string Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(nameof(Value)); }
        }

        public string Translation
        {
            get => _translation;
            set
            {
                _translation = value;
                OnPropertyChanged(nameof(Translation));
                OnPropertyChanged(nameof(StatusIcon));
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        /// <summary>静默设置 IsSelected（不触发 PropertyChanged），用于批量操作性能优化。</summary>
        public void SetIsSelectedSilent(bool value)
        {
            _isSelected = value;
        }

        /// <summary>是否命中黑名单前缀规则（命中条目在翻译时被跳过，状态列显示 🚫）。</summary>
        public bool IsBlacklisted
        {
            get => _isBlacklisted;
            set
            {
                _isBlacklisted = value;
                OnPropertyChanged(nameof(IsBlacklisted));
                OnPropertyChanged(nameof(StatusIcon));
            }
        }

        public ReviewStatus ReviewStatus
        {
            get => _reviewStatus;
            set
            {
                _reviewStatus = value;
                OnPropertyChanged(nameof(ReviewStatus));
                OnPropertyChanged(nameof(StatusIcon));
            }
        }

        /// <summary>
        /// AI 评估分数（0-10），-1 表示未评估。绑定到 DataGrid 的 Score 列。
        /// </summary>
        public double EvaluationScore
        {
            get => _evaluationScore;
            set
            {
                _evaluationScore = value;
                OnPropertyChanged(nameof(EvaluationScore));
                OnPropertyChanged(nameof(EvaluationScoreDisplay));
                OnPropertyChanged(nameof(EvaluationScoreColor));
            }
        }

        /// <summary>用于显示的分数文本，-1 显示空字符串。</summary>
        public string EvaluationScoreDisplay => _evaluationScore < 0 ? "" : $"{_evaluationScore:F1}";

        /// <summary>分数颜色：高分绿、中分黄、低分红、未评估灰。</summary>
        public string EvaluationScoreColor => _evaluationScore switch
        {
            >= 8 => "#2E7D32",
            >= 5 => "#F57F17",
            >= 0 => "#C62828",
            _ => "#9E9E9E"
        };

        /// <summary>AI 评估的改进建议，留空表示无建议。tooltip 显示。</summary>
        public string EvaluationImprovement
        {
            get => _evaluationImprovement;
            set { _evaluationImprovement = value; OnPropertyChanged(nameof(EvaluationImprovement)); }
        }

        public string StatusIcon
        {
            get
            {
                if (IsBlacklisted) return "🚫";
                return _reviewStatus switch
                {
                    ReviewStatus.Reviewed => "✅",
                    ReviewStatus.NeedsFix => "🔧",
                    ReviewStatus.NotReviewed => string.IsNullOrEmpty(Translation) ? "❌" : "📝",
                    _ => "❌"
                };
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
