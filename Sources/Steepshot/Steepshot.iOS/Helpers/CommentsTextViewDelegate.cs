﻿using System;
using Foundation;
using UIKit;

namespace Steepshot.iOS.Helpers
{
    class CommentsTextViewDelegate : BaseTextViewDelegate
    {
        public Action<nfloat> ChangedAction;

        public override bool ShouldChangeText(UITextView textView, NSRange range, string text)
        {
            if (text == "\n")
            {
                textView.ResignFirstResponder();
                return false;
            }
            return true;
        }

        public override void Changed(UITextView textView)
        {
            var size = textView.SizeThatFits(textView.Frame.Size).Height;
            if (size >= 98)
            {
                textView.ScrollEnabled = true;
                size = 98;
            }
            else
                textView.ScrollEnabled = false;
            ChangedAction?.Invoke(size);
        }
    }
}
