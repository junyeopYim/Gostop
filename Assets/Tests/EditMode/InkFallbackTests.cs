using System.Collections;
using System.Collections.Generic;
using Hwatu.View;
using Hwatu.View.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Hwatu.Core.Tests
{
    /// <summary>
    /// [B] 조용한 폴백 금지 검증: 셰이더 로드를 강제로 실패시킨 경로에서
    /// ① 경고 로그가 1회 찍히고 ② 와이프/페인트인이 대체 페이드로 완주해야 한다.
    /// EditMode에서는 Tween이 즉시 완료되므로 코루틴을 수동으로 소진해 검증한다.
    /// </summary>
    public sealed class InkFallbackTests
    {
        [SetUp]
        public void SetUp()
        {
            InkEffectResources.ForceShaderUnavailableForTests = true;
            InkEffectResources.ResetFallbackWarningForTests();
        }

        [TearDown]
        public void TearDown()
        {
            InkEffectResources.ForceShaderUnavailableForTests = false;
            InkEffectResources.ResetFallbackWarningForTests();
            var canvas = GameObject.Find("InkWipeTransitionCanvas");
            if (canvas != null) Object.DestroyImmediate(canvas);
        }

        [Test]
        public void 셰이더_부재_시_와이프는_경고_1회와_단색_페이드로_완주한다()
        {
            LogAssert.Expect(LogType.Warning, InkEffectResources.FallbackWarning);

            var wipe = new InkWipeTransition();
            Drain(wipe.Hide(), "Hide");

            var canvas = GameObject.Find("InkWipeTransitionCanvas");
            Assert.IsNotNull(canvas, "폴백에서도 오버레이 캔버스는 생성되어야 한다");
            var image = canvas.GetComponentInChildren<Image>(true);
            Assert.IsNotNull(image, "폴백에서도 가림 이미지는 생성되어야 한다");
            Assert.IsTrue(canvas.activeSelf, "Hide 후 오버레이가 화면을 가리고 있어야 한다");
            Assert.AreEqual(1f, image.color.a, 1e-3f, "Hide 폴백 페이드는 완전 가림(알파 1)까지 완주해야 한다");

            Drain(wipe.Reveal(), "Reveal");
            Assert.AreEqual(0f, image.color.a, 1e-3f, "Reveal 폴백 페이드는 완전 개방(알파 0)까지 완주해야 한다");
            Assert.IsFalse(canvas.activeSelf, "Reveal 후 오버레이는 꺼져야 한다");

            // 두 번째 와이프에서는 경고가 다시 찍히지 않는다 (1회 계약)
            Drain(wipe.Hide(), "Hide(2회차)");
            Drain(wipe.Reveal(), "Reveal(2회차)");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 셰이더_부재_시_페인트인은_경고와_알파_페이드인으로_대체된다()
        {
            LogAssert.Expect(LogType.Warning, InkEffectResources.FallbackWarning);

            var go = new GameObject("PaintTarget", typeof(RectTransform));
            try
            {
                var image = go.AddComponent<Image>();
                image.color = new Color(0.2f, 0.4f, 0.6f, 0.8f);
                var effect = go.AddComponent<PaintInEffect>();
                effect.Play(0.5f);

                // EditMode 트윈은 즉시 완주한다 — 페이드인 종료 후 원래 색·알파가 복원되어야 한다
                Assert.AreEqual(0.8f, image.color.a, 1e-3f, "폴백 페이드인 완주 후 알파가 원래 값이어야 한다");
                Assert.AreEqual(0.2f, image.color.r, 1e-3f, "폴백 페이드인이 색상을 훼손하면 안 된다");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void 정상_경로에서는_경고_없이_머티리얼이_생성된다()
        {
            InkEffectResources.ForceShaderUnavailableForTests = false;
            var material = InkEffectResources.CreateMaterial(InkMaskKind.SweepDiag);
            Assert.IsNotNull(material, "정상 경로에서는 InkDissolve 머티리얼이 생성되어야 한다");
            Object.DestroyImmediate(material);
            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>중첩 yield(내부 IEnumerator)까지 수동으로 소진한다.</summary>
        private static void Drain(IEnumerator routine, string what)
        {
            int guard = 0;
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            while (stack.Count > 0)
            {
                Assert.Less(guard++, 10000, $"{what}: 폴백 코루틴이 유한 스텝 안에 완주해야 한다");
                var top = stack.Peek();
                if (!top.MoveNext())
                {
                    stack.Pop();
                    continue;
                }
                if (top.Current is IEnumerator nested) stack.Push(nested);
            }
        }
    }
}
