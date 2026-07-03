using UnityEngine;
using UnityEngine.UI;

namespace Hwatu.View.Screens
{
    public sealed class EffectChipFlowLayout : LayoutGroup
    {
        public float Spacing = 8f;
        public float LineSpacing = 6f;
        public float RowHeight = 30f;

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            SetLayoutInputForAxis(0f, Width, -1f, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            SetLayoutInputForAxis(PreferredHeight(), PreferredHeight(), -1f, 1);
        }

        public override void SetLayoutHorizontal()
        {
            LayoutChildren();
        }

        public override void SetLayoutVertical()
        {
            LayoutChildren();
        }

        private float PreferredHeight()
        {
            int rows = CountRows(Width);
            if (rows <= 0) return RowHeight;
            return padding.vertical + rows * RowHeight + (rows - 1) * LineSpacing;
        }

        private void LayoutChildren()
        {
            float width = Width;
            float x = padding.left;
            float y = padding.top;
            for (int i = 0; i < rectChildren.Count; i++)
            {
                var child = rectChildren[i];
                float childWidth = Mathf.Min(LayoutUtility.GetPreferredWidth(child), width - padding.horizontal);
                if (x > padding.left && x + childWidth > width - padding.right)
                {
                    x = padding.left;
                    y += RowHeight + LineSpacing;
                }

                SetChildAlongAxis(child, 0, x, childWidth);
                SetChildAlongAxis(child, 1, y, RowHeight);
                x += childWidth + Spacing;
            }
        }

        private int CountRows(float width)
        {
            if (rectChildren.Count == 0) return 1;
            int rows = 1;
            float x = padding.left;
            for (int i = 0; i < rectChildren.Count; i++)
            {
                float childWidth = Mathf.Min(LayoutUtility.GetPreferredWidth(rectChildren[i]), width - padding.horizontal);
                if (x > padding.left && x + childWidth > width - padding.right)
                {
                    rows++;
                    x = padding.left;
                }
                x += childWidth + Spacing;
            }
            return rows;
        }

        private float Width => Mathf.Max(1f, rectTransform.rect.width > 1f ? rectTransform.rect.width : 850f);
    }
}
