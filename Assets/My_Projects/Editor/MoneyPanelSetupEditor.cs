#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using NitroStreets.Ads;

namespace NitroStreets.EditorTools
{
    [InitializeOnLoad]
    public static class MoneyPanelSetupEditor
    {
        private const int UILayer = 5;

        static MoneyPanelSetupEditor()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorSceneManager.GetActiveScene().name == "Garage")
                {
                    SyncMoneyPanelsAndPopup(false);
                }
            };
        }

        [MenuItem("Tools/NitroStreets/Синхронизировать панель монет и окно рекламы (Sync Coins UI)", priority = 101)]
        public static void SyncMoneyPanelsAndPopupMenuItem()
        {
            SyncMoneyPanelsAndPopup(true);
        }

        public static void SyncMoneyPanelsAndPopup(bool showNotification)
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.name != "Garage")
            {
                if (showNotification)
                    EditorUtility.DisplayDialog("NitroStreets", "Пожалуйста, откройте сцену 'Garage' перед синхронизацией.", "OK");
                return;
            }

            Canvas mainCanvas = Object.FindFirstObjectByType<Canvas>();
            if (mainCanvas == null)
            {
                Debug.LogWarning("[MoneyPanelSetup] Canvas не найден в сцене Garage.");
                return;
            }

            bool modifiedAny = false;
            Font sharedFont = ResolveSharedFont(mainCanvas);

            // 1. Находим эталонную панель (Money_Panel из Main_Menu или панель с Add_Money_Button)
            Transform sourceMoneyPanel = FindMasterMoneyPanel(mainCanvas);
            if (sourceMoneyPanel == null)
            {
                Debug.LogError("[MoneyPanelSetup] Не найдена эталонная панель Money_Panel в Main_Menu!");
                return;
            }

            Transform sourceIcon = sourceMoneyPanel.Find("Icon");
            Transform sourceButton = sourceMoneyPanel.Find("Add_Money_Button");
            if (sourceButton == null)
            {
                sourceButton = sourceMoneyPanel.Find("Add_Coins_Button");
            }

            if (sourceButton == null)
            {
                Debug.LogError("[MoneyPanelSetup] На панели Money_Panel не найдена кнопка добавления денег (Add_Money_Button)!");
                return;
            }

            // Переводим эталонную кнопку и её дочерние элементы в слой UI (Layer 5)
            SetLayerRecursively(sourceButton.gameObject, UILayer, ref modifiedAny);

            // 2. Создаём или проверяем модальное окно предложения рекламы (Coins_Ad_Offer_Popup)
            CoinsRewardUI coinsUI = EnsureCoinsAdOfferPopup(mainCanvas, sharedFont, ref modifiedAny);
            if (coinsUI == null)
            {
                Debug.LogError("[MoneyPanelSetup] Не удалось настроить CoinsRewardUI / Coins_Ad_Offer_Popup!");
                return;
            }

            // Привязываем клик эталонной кнопки Add_Money_Button к открытию окна рекламы
            Button srcBtnComp = sourceButton.GetComponent<Button>();
            if (srcBtnComp != null)
            {
                BindButtonOnClick(srcBtnComp, coinsUI, ref modifiedAny);
            }

            // 3. Находим все остальные меню, где должны отображаться деньги
            List<Transform> targetHeaders = FindAllMenuTopHeaders(mainCanvas, sourceMoneyPanel);

            int syncedCount = 1; // эталон уже учтен
            foreach (Transform topHeader in targetHeaders)
            {
                if (topHeader == null) continue;

                // Ищем в этом Top панель денег (Money_Panel или Coins)
                Transform targetPanel = topHeader.Find("Money_Panel");
                if (targetPanel == null)
                {
                    targetPanel = topHeader.Find("Coins");
                }

                if (targetPanel == null)
                {
                    Debug.LogWarning($"[MoneyPanelSetup] В заголовке '{topHeader.parent?.name}/{topHeader.name}' не найдена панель денег.");
                    continue;
                }

                Undo.RecordObject(targetPanel.gameObject, "Sync Money Panel");
                targetPanel.name = "Money_Panel";
                SetLayerRecursively(targetPanel.gameObject, UILayer, ref modifiedAny);

                // Синхронизируем размер панели с эталоном (337 x 88)
                RectTransform targetPanelRt = targetPanel.GetComponent<RectTransform>();
                RectTransform srcPanelRt = sourceMoneyPanel.GetComponent<RectTransform>();
                if (targetPanelRt != null && srcPanelRt != null)
                {
                    targetPanelRt.sizeDelta = srcPanelRt.sizeDelta;
                    targetPanelRt.anchoredPosition = srcPanelRt.anchoredPosition;
                    targetPanelRt.anchorMin = srcPanelRt.anchorMin;
                    targetPanelRt.anchorMax = srcPanelRt.anchorMax;
                    targetPanelRt.pivot = srcPanelRt.pivot;
                    modifiedAny = true;
                }

                Image targetPanelImg = targetPanel.GetComponent<Image>();
                Image srcPanelImg = sourceMoneyPanel.GetComponent<Image>();
                if (targetPanelImg != null && srcPanelImg != null)
                {
                    targetPanelImg.sprite = srcPanelImg.sprite;
                    targetPanelImg.raycastTarget = false;
                    modifiedAny = true;
                }

                // Удаляем временные Add_Coins_Button от предыдущих тестов, если есть
                Transform oldTempBtn = targetPanel.Find("Add_Coins_Button");
                if (oldTempBtn != null && oldTempBtn != sourceButton)
                {
                    Undo.DestroyObjectImmediate(oldTempBtn.gameObject);
                    modifiedAny = true;
                }

                // Синхронизируем иконку пачки денег (Icon)
                Transform targetIcon = targetPanel.Find("Icon");
                if (targetIcon == null && sourceIcon != null)
                {
                    GameObject clonedIcon = Object.Instantiate(sourceIcon.gameObject, targetPanel);
                    Undo.RegisterCreatedObjectUndo(clonedIcon, "Clone Money Icon");
                    clonedIcon.name = "Icon";
                    targetIcon = clonedIcon.transform;
                    targetIcon.SetSiblingIndex(0);
                    modifiedAny = true;
                }

                if (targetIcon != null && sourceIcon != null)
                {
                    RectTransform tIconRt = targetIcon.GetComponent<RectTransform>();
                    RectTransform sIconRt = sourceIcon.GetComponent<RectTransform>();
                    tIconRt.anchorMin = sIconRt.anchorMin;
                    tIconRt.anchorMax = sIconRt.anchorMax;
                    tIconRt.pivot = sIconRt.pivot;
                    tIconRt.anchoredPosition = sIconRt.anchoredPosition;
                    tIconRt.sizeDelta = sIconRt.sizeDelta;
                    SetLayerRecursively(targetIcon.gameObject, UILayer, ref modifiedAny);
                }

                // Находим существующий Total_Coins_Text (сохраняем оригинальный GameObject и компонент!)
                Transform totalCoinsTextTr = null;
                for (int i = 0; i < targetPanel.childCount; i++)
                {
                    Transform c = targetPanel.GetChild(i);
                    if (c.name.Contains("Total_Coins") || c.name.Contains("Coin") || c.name.Contains("Score") || c.name.Contains("Text"))
                    {
                        if (c.GetComponent<Text>() != null)
                        {
                            totalCoinsTextTr = c;
                            break;
                        }
                    }
                }

                if (totalCoinsTextTr != null)
                {
                    Transform srcTextTr = sourceMoneyPanel.Find("Total_Coins_Text");
                    if (srcTextTr != null)
                    {
                        RectTransform tTextRt = totalCoinsTextTr.GetComponent<RectTransform>();
                        RectTransform sTextRt = srcTextTr.GetComponent<RectTransform>();
                        tTextRt.anchorMin = sTextRt.anchorMin;
                        tTextRt.anchorMax = sTextRt.anchorMax;
                        tTextRt.pivot = sTextRt.pivot;
                        tTextRt.anchoredPosition = sTextRt.anchoredPosition;
                        tTextRt.sizeDelta = sTextRt.sizeDelta;
                        modifiedAny = true;
                    }
                    SetLayerRecursively(totalCoinsTextTr.gameObject, UILayer, ref modifiedAny);
                }

                // Синхронизируем кнопку Add_Money_Button
                Transform targetBtn = targetPanel.Find("Add_Money_Button");
                if (targetBtn == null)
                {
                    GameObject clonedBtn = Object.Instantiate(sourceButton.gameObject, targetPanel);
                    Undo.RegisterCreatedObjectUndo(clonedBtn, "Clone Add_Money_Button");
                    clonedBtn.name = "Add_Money_Button";
                    targetBtn = clonedBtn.transform;
                    modifiedAny = true;
                }

                if (targetBtn != null)
                {
                    RectTransform tBtnRt = targetBtn.GetComponent<RectTransform>();
                    RectTransform sBtnRt = sourceButton.GetComponent<RectTransform>();
                    tBtnRt.anchorMin = sBtnRt.anchorMin;
                    tBtnRt.anchorMax = sBtnRt.anchorMax;
                    tBtnRt.pivot = sBtnRt.pivot;
                    tBtnRt.anchoredPosition = sBtnRt.anchoredPosition;
                    tBtnRt.sizeDelta = sBtnRt.sizeDelta;
                    SetLayerRecursively(targetBtn.gameObject, UILayer, ref modifiedAny);

                    Button targetBtnComp = targetBtn.GetComponent<Button>();
                    if (targetBtnComp != null)
                    {
                        BindButtonOnClick(targetBtnComp, coinsUI, ref modifiedAny);
                    }
                }

                syncedCount++;
            }

            if (modifiedAny)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                Debug.Log($"[MoneyPanelSetup] Панель денег 'Money_Panel' и кнопка рекламы успешно настроены во всех {syncedCount} меню!");

                if (showNotification)
                {
                    EditorUtility.DisplayDialog("NitroStreets",
                        $"Готово!\n\nМодифицированная панель денег с кнопкой '+' и окно просмотра рекламы (+500 $) успешно синхронизированы для всех {syncedCount} меню Гаража.",
                        "OK");
                }
            }
            else
            {
                if (showNotification)
                {
                    EditorUtility.DisplayDialog("NitroStreets",
                        $"Все {syncedCount} меню уже полностью синхронизированы и настроены.",
                        "OK");
                }
            }
        }

        private static Transform FindMasterMoneyPanel(Canvas canvas)
        {
            // 1. Приоритетно ищем Money_Panel в Main_Menu
            Transform mainMenu = canvas.transform.Find("Main_Menu");
            if (mainMenu != null)
            {
                Transform top = mainMenu.Find("Top");
                if (top != null)
                {
                    Transform p = top.Find("Money_Panel");
                    if (p != null) return p;
                }
            }

            // 2. Ищем любую панель с Add_Money_Button
            Transform[] all = canvas.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in all)
            {
                if (t != null && t.name == "Add_Money_Button")
                {
                    return t.parent;
                }
            }

            // 3. Fallback: любая панель с именем Money_Panel
            foreach (Transform t in all)
            {
                if (t != null && t.name == "Money_Panel")
                {
                    return t;
                }
            }

            return null;
        }

        private static List<Transform> FindAllMenuTopHeaders(Canvas canvas, Transform sourceMoneyPanel)
        {
            var result = new List<Transform>();
            string[] menuPaths = new string[]
            {
                "Car_Select/Top",
                "Formula1_Car_Select/Top",
                "Career_Select_Level/Levels_Panel/Top",
                "Formula1_Select_Level/Levels_Panel/Top",
                "Career_Mode/Car_Select/Top",
                "Career_Mode/Career_Select_Level/Levels_Panel/Top",
                "Formula1_Mode/Formula1_Car_Select/Top",
                "Formula1_Mode/Formula1_Select_Level/Levels_Panel/Top",
                "Offroad_Select_Level/Levels_Panel/Top",
                "Mode_Select/Top",
                "Main_Menu/Top"
            };

            foreach (string p in menuPaths)
            {
                Transform top = canvas.transform.Find(p);
                if (top != null)
                {
                    // Исключаем заголовок эталонной панели, чтобы не дублировать
                    if (sourceMoneyPanel != null && sourceMoneyPanel.parent == top)
                        continue;

                    result.Add(top);
                }
            }

            return result;
        }

        private static void BindButtonOnClick(Button btn, CoinsRewardUI coinsUI, ref bool modified)
        {
            Undo.RecordObject(btn, "Bind Click to CoinsRewardUI");

            // Очищаем существующие привязки к ShowOfferPopup, чтобы не дублировать
            for (int i = btn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                if (btn.onClick.GetPersistentMethodName(i) == nameof(CoinsRewardUI.ShowOfferPopup))
                {
                    UnityEventTools.RemovePersistentListener(btn.onClick, i);
                    modified = true;
                }
            }

            UnityAction call = new UnityAction(coinsUI.ShowOfferPopup);
            UnityEventTools.AddPersistentListener(btn.onClick, call);
            EditorUtility.SetDirty(btn);
            modified = true;
        }

        private static CoinsRewardUI EnsureCoinsAdOfferPopup(Canvas canvas, Font font, ref bool modified)
        {
            CoinsRewardUI coinsUI = canvas.GetComponentInChildren<CoinsRewardUI>(true);
            GameObject popupRoot = null;

            if (coinsUI != null)
            {
                popupRoot = coinsUI.gameObject;
            }
            else
            {
                Transform existing = canvas.transform.Find("Coins_Ad_Offer_Popup");
                if (existing != null)
                {
                    popupRoot = existing.gameObject;
                    coinsUI = popupRoot.GetComponent<CoinsRewardUI>() ?? popupRoot.AddComponent<CoinsRewardUI>();
                }
            }

            Sprite coinPackSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/My_Projects/Textures/Racing_UI_512x512_PNG/PNG_512x512/icon_currency_pack.png");
            if (coinPackSprite == null)
            {
                coinPackSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/My_Projects/Textures/Racing_UI_512x512_PNG/PNG_512x512/currency_gold.png");
            }

            if (popupRoot == null)
            {
                popupRoot = new GameObject("Coins_Ad_Offer_Popup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                Undo.RegisterCreatedObjectUndo(popupRoot, "Create Coins_Ad_Offer_Popup");
                popupRoot.transform.SetParent(canvas.transform, false);

                RectTransform rootRt = popupRoot.GetComponent<RectTransform>();
                rootRt.anchorMin = Vector2.zero;
                rootRt.anchorMax = Vector2.one;
                rootRt.offsetMin = Vector2.zero;
                rootRt.offsetMax = Vector2.zero;

                Image overlayImg = popupRoot.GetComponent<Image>();
                overlayImg.color = new Color(0f, 0f, 0f, 0.78f);
                overlayImg.raycastTarget = true;

                // Центральное окно Dialog_Box
                GameObject dialogObj = new GameObject("Dialog_Box", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                dialogObj.transform.SetParent(popupRoot.transform, false);

                RectTransform dialogRt = dialogObj.GetComponent<RectTransform>();
                dialogRt.anchorMin = new Vector2(0.5f, 0.5f);
                dialogRt.anchorMax = new Vector2(0.5f, 0.5f);
                dialogRt.pivot = new Vector2(0.5f, 0.5f);
                dialogRt.anchoredPosition = Vector2.zero;
                dialogRt.sizeDelta = new Vector2(640f, 380f);

                Image dialogImg = dialogObj.GetComponent<Image>();
                dialogImg.color = new Color(0.08f, 0.11f, 0.16f, 0.98f);

                Outline dialogOutline = dialogObj.AddComponent<Outline>();
                dialogOutline.effectColor = new Color(1f, 0.75f, 0.12f, 0.92f);
                dialogOutline.effectDistance = new Vector2(3f, -3f);

                // Заголовок Title_Text
                GameObject titleObj = new GameObject("Title_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                titleObj.transform.SetParent(dialogObj.transform, false);
                RectTransform titleRt = titleObj.GetComponent<RectTransform>();
                titleRt.anchorMin = new Vector2(0.05f, 0.76f);
                titleRt.anchorMax = new Vector2(0.95f, 0.95f);
                titleRt.offsetMin = Vector2.zero;
                titleRt.offsetMax = Vector2.zero;

                Text titleTxt = titleObj.GetComponent<Text>();
                titleTxt.font = font;
                titleTxt.text = "FREE COINS";
                titleTxt.alignment = TextAnchor.MiddleCenter;
                titleTxt.color = new Color(1f, 0.84f, 0.2f, 1f);
                titleTxt.fontStyle = FontStyle.Bold;
                titleTxt.resizeTextForBestFit = true;
                titleTxt.resizeTextMinSize = 18;
                titleTxt.resizeTextMaxSize = 38;

                // Иконка Reward_Icon
                if (coinPackSprite != null)
                {
                    GameObject iconObj = new GameObject("Reward_Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    iconObj.transform.SetParent(dialogObj.transform, false);
                    RectTransform iconRt = iconObj.GetComponent<RectTransform>();
                    iconRt.anchorMin = new Vector2(0.5f, 0.58f);
                    iconRt.anchorMax = new Vector2(0.5f, 0.58f);
                    iconRt.pivot = new Vector2(0.5f, 0.5f);
                    iconRt.anchoredPosition = Vector2.zero;
                    iconRt.sizeDelta = new Vector2(86f, 86f);

                    Image iconImg = iconObj.GetComponent<Image>();
                    iconImg.sprite = coinPackSprite;
                    iconImg.preserveAspect = true;
                }

                // Статус-сообщение Status_Text
                GameObject statusObj = new GameObject("Status_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                statusObj.transform.SetParent(dialogObj.transform, false);
                RectTransform statusRt = statusObj.GetComponent<RectTransform>();
                statusRt.anchorMin = new Vector2(0.06f, 0.32f);
                statusRt.anchorMax = new Vector2(0.94f, 0.52f);
                statusRt.offsetMin = Vector2.zero;
                statusRt.offsetMax = Vector2.zero;

                Text statusTxt = statusObj.GetComponent<Text>();
                statusTxt.font = font;
                statusTxt.text = "Watch a short video ad\nto receive +500 Coins!";
                statusTxt.alignment = TextAnchor.MiddleCenter;
                statusTxt.color = Color.white;
                statusTxt.resizeTextForBestFit = true;
                statusTxt.resizeTextMinSize = 14;
                statusTxt.resizeTextMaxSize = 24;

                // Кнопка Watch_Ad_Button
                GameObject watchBtnObj = new GameObject("Watch_Ad_Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                watchBtnObj.transform.SetParent(dialogObj.transform, false);
                RectTransform watchBtnRt = watchBtnObj.GetComponent<RectTransform>();
                watchBtnRt.anchorMin = new Vector2(0.5f, 0.16f);
                watchBtnRt.anchorMax = new Vector2(0.5f, 0.16f);
                watchBtnRt.pivot = new Vector2(0.5f, 0.5f);
                watchBtnRt.anchoredPosition = Vector2.zero;
                watchBtnRt.sizeDelta = new Vector2(300f, 60f);

                Image watchBtnImg = watchBtnObj.GetComponent<Image>();
                watchBtnImg.color = new Color(0.12f, 0.78f, 0.35f, 1f);

                Outline watchBtnOutline = watchBtnObj.AddComponent<Outline>();
                watchBtnOutline.effectColor = new Color(0.6f, 1f, 0.7f, 0.7f);
                watchBtnOutline.effectDistance = new Vector2(2f, -2f);

                Button watchBtn = watchBtnObj.GetComponent<Button>();
                watchBtn.targetGraphic = watchBtnImg;

                GameObject watchBtnTxtObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                watchBtnTxtObj.transform.SetParent(watchBtnObj.transform, false);
                RectTransform watchBtnTxtRt = watchBtnTxtObj.GetComponent<RectTransform>();
                watchBtnTxtRt.anchorMin = Vector2.zero;
                watchBtnTxtRt.anchorMax = Vector2.one;
                watchBtnTxtRt.offsetMin = Vector2.zero;
                watchBtnTxtRt.offsetMax = Vector2.zero;

                Text watchBtnTxt = watchBtnTxtObj.GetComponent<Text>();
                watchBtnTxt.font = font;
                watchBtnTxt.text = "WATCH AD (+500 $)";
                watchBtnTxt.alignment = TextAnchor.MiddleCenter;
                watchBtnTxt.color = Color.white;
                watchBtnTxt.fontStyle = FontStyle.Bold;
                watchBtnTxt.resizeTextForBestFit = true;
                watchBtnTxt.resizeTextMinSize = 14;
                watchBtnTxt.resizeTextMaxSize = 26;

                // Кнопка закрытия Close_Button
                GameObject closeBtnObj = new GameObject("Close_Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                closeBtnObj.transform.SetParent(dialogObj.transform, false);
                RectTransform closeBtnRt = closeBtnObj.GetComponent<RectTransform>();
                closeBtnRt.anchorMin = new Vector2(1f, 1f);
                closeBtnRt.anchorMax = new Vector2(1f, 1f);
                closeBtnRt.pivot = new Vector2(1f, 1f);
                closeBtnRt.anchoredPosition = new Vector2(-10f, -10f);
                closeBtnRt.sizeDelta = new Vector2(46f, 46f);

                Image closeBtnImg = closeBtnObj.GetComponent<Image>();
                closeBtnImg.color = new Color(0.78f, 0.18f, 0.18f, 0.95f);

                Button closeBtn = closeBtnObj.GetComponent<Button>();
                closeBtn.targetGraphic = closeBtnImg;

                GameObject closeTxtObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                closeTxtObj.transform.SetParent(closeBtnObj.transform, false);
                RectTransform closeTxtRt = closeTxtObj.GetComponent<RectTransform>();
                closeTxtRt.anchorMin = Vector2.zero;
                closeTxtRt.anchorMax = Vector2.one;
                closeTxtRt.offsetMin = Vector2.zero;
                closeTxtRt.offsetMax = Vector2.zero;

                Text closeTxt = closeTxtObj.GetComponent<Text>();
                closeTxt.font = font;
                closeTxt.text = "X";
                closeTxt.alignment = TextAnchor.MiddleCenter;
                closeTxt.color = Color.white;
                closeTxt.fontStyle = FontStyle.Bold;
                closeTxt.resizeTextForBestFit = true;
                closeTxt.resizeTextMinSize = 16;
                closeTxt.resizeTextMaxSize = 30;

                coinsUI = popupRoot.AddComponent<CoinsRewardUI>();
                coinsUI.SetReferences(popupRoot, watchBtn, closeBtn, titleTxt, statusTxt);
            }

            // Переводим всплывающее окно и все его дочерние элементы в слой UI (Layer 5)
            SetLayerRecursively(popupRoot, UILayer, ref modified);

            popupRoot.SetActive(false);
            modified = true;
            return coinsUI;
        }

        private static void SetLayerRecursively(GameObject obj, int newLayer, ref bool modified)
        {
            if (obj == null) return;
            if (obj.layer != newLayer)
            {
                Undo.RecordObject(obj, "Change Layer to UI");
                obj.layer = newLayer;
                modified = true;
            }

            for (int i = 0; i < obj.transform.childCount; i++)
            {
                SetLayerRecursively(obj.transform.GetChild(i).gameObject, newLayer, ref modified);
            }
        }

        private static Font ResolveSharedFont(Canvas canvas)
        {
            Text[] texts = canvas.GetComponentsInChildren<Text>(true);
            foreach (var t in texts)
            {
                if (t != null && t.font != null)
                    return t.font;
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
#endif
