﻿using System;
using CoreGraphics;
using PureLayout.Net;
using Steepshot.iOS.ViewControllers;
using UIKit;

namespace Steepshot.iOS.CustomViews
{
    public enum AnimationType
    {
        Bottom
    }

    public class CustomAlertView
    {
        private UIViewController controller;
        private UIView popup;
        private UIView dialog;
        private nfloat targetY;

        public CustomAlertView(UIView view, UIViewController controller, AnimationType animationType = AnimationType.Bottom)
        {
            dialog = view;
            this.controller = controller;

            popup = new UIView();
            popup.Frame = new CGRect(0, 0, UIScreen.MainScreen.Bounds.Width, UIScreen.MainScreen.Bounds.Height);
            popup.BackgroundColor = UIColor.Black.ColorWithAlpha(0.0f);
            popup.UserInteractionEnabled = true;

            popup.AddSubview(view);

            if (controller is InteractivePopNavigationController interactiveController)
                interactiveController.IsPushingViewController = true;

            controller.View.AddSubview(popup);

            // view centering
            view.AutoPinEdgeToSuperviewEdge(ALEdge.Bottom, 34);
            view.AutoAlignAxisToSuperviewAxis(ALAxis.Vertical);

            targetY = view.Frame.Y;
            SetAnimation(animationType);
        }

        public void Hide()
        {
            UIView.Animate(0.3, () =>
            {
                popup.BackgroundColor = UIColor.Black.ColorWithAlpha(0.0f);
                dialog.Transform = CGAffineTransform.Translate(CGAffineTransform.MakeIdentity(), 0, UIScreen.MainScreen.Bounds.Bottom);
            }, () =>
            {
                if (controller is InteractivePopNavigationController interactiveController)
                    interactiveController.IsPushingViewController = false;
                popup.RemoveFromSuperview();
            });

        }

        private void SetAnimation(AnimationType animationType )
        {
            switch (animationType)
            {
                case AnimationType.Bottom:
                    dialog.Transform = CGAffineTransform.Translate(CGAffineTransform.MakeIdentity(), 0, UIScreen.MainScreen.Bounds.Bottom);

                    UIView.Animate(0.3, () =>
                    {
                        popup.BackgroundColor = UIColor.Black.ColorWithAlpha(0.5f);
                        dialog.Transform = CGAffineTransform.Translate(CGAffineTransform.MakeIdentity(), 0, targetY - 10);
                    }, () =>
                    {
                        UIView.Animate(0.1, () =>
                        {
                            dialog.Transform = CGAffineTransform.Translate(CGAffineTransform.MakeIdentity(), 0, targetY);
                        });
                    });
                    break;
            }
        }
    }
}
