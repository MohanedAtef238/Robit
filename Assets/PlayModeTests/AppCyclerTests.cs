using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Robit.Tests.PlayMode
{
    public class AppCyclerTests
    {
        private GameObject _holder;
        private AppCyclerController _controller;
        private UIDocument _uiDoc;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            _holder = new GameObject("TestCycler");
            _uiDoc = _holder.AddComponent<UIDocument>();
            
#if UNITY_EDITOR
            _uiDoc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/AppCycler.uxml");
#endif
            
            _controller = _holder.AddComponent<AppCyclerController>();
            
            // Assign the serialized field via reflection or assume it picks up the UIDocument's visualTreeAsset?
            // AppCyclerController doesn't use the UIDocument's asset directly, it instantiates cyclerUXML.
            // Let's use reflection to set cyclerUXML.
#if UNITY_EDITOR
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/AppCycler.uxml");
            var field = typeof(AppCyclerController).GetField("cyclerUXML", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
                field.SetValue(_controller, uxml);
            
            var cardUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/DesktopCard.uxml");
            var cardField = typeof(AppCyclerController).GetField("cardTemplate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (cardField != null)
                cardField.SetValue(_controller, cardUxml);
#endif

            // Wait for Start and UI bindings
            yield return null;
        }

        [TearDown]
        public void Teardown()
        {
            if (_holder != null)
                Object.DestroyImmediate(_holder);
        }

        [UnityTest]
        public IEnumerator InitialState_IsTucked()
        {
            // Assert
            var dock = _uiDoc.rootVisualElement.Q<VisualElement>("cycler-dock");
            Assert.IsNotNull(dock, "cycler-dock should be present");
            
            // Should have the tucked class
            Assert.IsTrue(dock.ClassListContains("cycler-dock--tucked"), "Dock should start in tucked state");
            
            yield return null;
        }

        [UnityTest]
        public IEnumerator HoverEnter_PeeksDock()
        {
            var dock = _uiDoc.rootVisualElement.Q<VisualElement>("cycler-dock");
            var hitArea = _uiDoc.rootVisualElement.Q<VisualElement>("cycler-hit-area");
            
            Assert.IsNotNull(dock);
            Assert.IsNotNull(hitArea);

            // Act
            using (var e = PointerEnterEvent.GetPooled())
            {
                e.target = hitArea;
                hitArea.SendEvent(e);
            }
            
            yield return null;

            // Assert
            Assert.IsTrue(dock.ClassListContains("cycler-dock--peeked"), "Dock should transition to peeked state on hover");
            Assert.IsFalse(dock.ClassListContains("cycler-dock--tucked"));
        }

        [UnityTest]
        public IEnumerator HoverLeave_TucksDock()
        {
            var dock = _uiDoc.rootVisualElement.Q<VisualElement>("cycler-dock");
            var hitArea = _uiDoc.rootVisualElement.Q<VisualElement>("cycler-hit-area");

            // Act - Hover enter
            using (var e = PointerEnterEvent.GetPooled()) { e.target = hitArea; hitArea.SendEvent(e); }
            yield return null;
            
            // Act - Hover leave
            using (var e = PointerLeaveEvent.GetPooled()) { e.target = hitArea; hitArea.SendEvent(e); }
            yield return null;

            // Assert
            Assert.IsTrue(dock.ClassListContains("cycler-dock--tucked"), "Dock should return to tucked state after hover leave");
        }

        [UnityTest]
        public IEnumerator Open_TransitionsToExpanded()
        {
            var dock = _uiDoc.rootVisualElement.Q<VisualElement>("cycler-dock");
            
            // Act
            _controller.Open();
            yield return null;

            // Assert
            Assert.IsTrue(dock.ClassListContains("cycler-dock--expanded"), "Dock should transition to expanded state on Open()");
        }
    }
}
