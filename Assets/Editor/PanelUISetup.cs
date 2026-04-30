using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using VectorFieldUI;

/// <summary>
/// Adds the missing inputScaleX, inputScaleY, and dropZone UI controls
/// to the Panel prefab and wires them to PanelController.
/// Run once: Menu → Panel → Add Missing Dropdowns
/// </summary>
public class PanelUISetup
{
    [MenuItem("Panel/Add Missing Dropdowns")]
    public static void AddMissingDropdowns()
    {
        const string prefabPath = "Assets/Prefabs/Panel.prefab";

        using var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath);
        GameObject root = scope.prefabContentsRoot;

        PanelController pc = root.GetComponentInChildren<PanelController>(true);
        if (pc == null) { Debug.LogError("[PanelSetup] PanelController not found."); return; }

        EnsureMissingDropdowns(pc);
        BeautifyPanelLayout(pc);
        RemoveConflictingTrackedRaycasters(root);
        Debug.Log("[PanelSetup] Done! Rebuild/run to populate dropdown options via PanelController.InitDropdowns.");
    }

    [MenuItem("Panel/Fix Panel Prefab (All)")]
    public static void FixPanelPrefabAll()
    {
        AddMissingDropdowns();
    }

    [MenuItem("Panel/Fix Panel In Open Scenes")]
    public static void FixPanelInOpenScenes()
    {
        bool changed = false;

        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var scene = EditorSceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;

            foreach (var root in scene.GetRootGameObjects())
            {
                var controllers = root.GetComponentsInChildren<PanelController>(true);
                foreach (var pc in controllers)
                {
                    if (pc == null)
                        continue;

                    Undo.RecordObject(pc, "Fix PanelController refs");
                    bool localChange = EnsureMissingDropdowns(pc);
                    localChange |= BeautifyPanelLayout(pc);
                    localChange |= RemoveConflictingTrackedRaycasters(pc.gameObject);
                    if (localChange)
                    {
                        EditorUtility.SetDirty(pc);
                        changed = true;
                    }
                }
            }

            if (changed)
                EditorSceneManager.MarkSceneDirty(scene);
        }

        if (changed)
            Debug.Log("[PanelSetup] Open scenes fixed (dropdowns + raycasters). Save scenes to persist changes.");
        else
            Debug.Log("[PanelSetup] No scene panel changes were needed.");
    }

    static bool EnsureMissingDropdowns(PanelController pc)
    {
        bool changed = false;
        TMP_Dropdown existingDrop = pc.dropCount ?? pc.dropFormula;
        if (existingDrop == null)
        {
            Debug.LogError("[PanelSetup] No existing dropdown found to clone.", pc);
            return false;
        }

        Transform content = existingDrop.transform.parent;

        // Reemplazar dropdowns con inputs de texto para X/Y
        if (pc.inputScaleX == null)
        {
            pc.inputScaleX = FindOrCreateInputField(content, "InputScaleX", "Multiplicador X");
            changed = true;
            Debug.Log("[PanelSetup] inputScaleX created/linked.");
        }

        if (pc.inputScaleY == null)
        {
            pc.inputScaleY = FindOrCreateInputField(content, "InputScaleY", "Multiplicador Y");
            changed = true;
            Debug.Log("[PanelSetup] inputScaleY created/linked.");
        }

        if (pc.dropZone == null)
        {
            pc.dropZone = FindOrCreateDropdown(content, "DropdownZone", "Zona");
            changed = true;
            Debug.Log("[PanelSetup] dropZone created/linked.");
        }

        if (content is RectTransform rt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

        return changed;
    }

    static bool BeautifyPanelLayout(PanelController pc)
    {
        bool changed = false;

        var panelCanvasRt = pc.GetComponent<RectTransform>();
        if (panelCanvasRt != null)
        {
            var targetSize = new Vector2(Mathf.Max(panelCanvasRt.sizeDelta.x, 480f), 650f);
            if (panelCanvasRt.sizeDelta != targetSize)
            {
                panelCanvasRt.sizeDelta = targetSize;
                changed = true;
            }
        }

        Transform content = pc.dropCount != null ? pc.dropCount.transform.parent : null;
        if (content == null)
            return changed;

        EnsureDropdownVisualSafety(pc.dropCount, ref changed);
        EnsureDropdownVisualSafety(pc.dropFormula, ref changed);
        EnsureDropdownVisualSafety(pc.dropZone, ref changed);

        var vlg = content.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            if (vlg.spacing != 2f)
            {
                vlg.spacing = 2f;
                changed = true;
            }

            if (vlg.padding.top != 4 || vlg.padding.bottom != 12 || vlg.padding.left != 12 || vlg.padding.right != 12)
            {
                vlg.padding.top = 4;
                vlg.padding.bottom = 12;
                vlg.padding.left = 12;
                vlg.padding.right = 12;
                changed = true;
            }

            if (!vlg.childControlWidth || !vlg.childControlHeight || !vlg.childForceExpandWidth)
            {
                vlg.childControlWidth = true;
                vlg.childControlHeight = true;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
                changed = true;
            }
        }

        var titleLabel = EnsureTitleLabel(content, ref changed);
        var descriptionLabel = EnsureDescriptionLabel(content, ref changed);
        if (descriptionLabel != null && descriptionLabel.gameObject.activeSelf)
        {
            descriptionLabel.gameObject.SetActive(false);
            changed = true;
        }

        var sectionFunctions = EnsureSimpleLabel(content, "FunctionsSectionLabel", "Funciones:", 20f, FontStyles.Bold, ref changed);

        var countLabel = EnsureLabelForDropdown(content, pc.dropCount, "CountFunctionLabel", "Cantidad de Vectores", ref changed);
        var countDesc = EnsureDescriptionSmall(content, "CountDesc", "Elige cuántos vectores mostrar en el campo", ref changed);
        
        var formulaLabel = EnsureLabelForDropdown(content, pc.dropFormula, "FormulaFunctionLabel", "Fórmula", ref changed);
        var formulaDesc = EnsureDescriptionSmall(content, "FormulaDesc", "Selecciona el patrón de dirección de los vectores", ref changed);
        
        var scaleXLabel = EnsureLabelForInputField(content, pc.inputScaleX, "InputScaleX_Label", "Escala X", ref changed);
        var scaleXDesc = EnsureDescriptionSmall(content, "ScaleXDesc", "Multiplicador para el eje X (ingresa cualquier número)", ref changed);
        
        var scaleYLabel = EnsureLabelForInputField(content, pc.inputScaleY, "InputScaleY_Label", "Escala Y", ref changed);
        var scaleYDesc = EnsureDescriptionSmall(content, "ScaleYDesc", "Multiplicador para el eje Y (ingresa cualquier número)", ref changed);
        
        var zoneLabel = EnsureLabelForDropdown(content, pc.dropZone, "DropdownZone_Label", "Zona", ref changed);
        var zoneDesc = EnsureDescriptionSmall(content, "ZoneDesc", "Elige el área del océano donde mostrar los vectores", ref changed);

        changed |= DisableLegacyDuplicateLabels(content, titleLabel, sectionFunctions, countLabel, countDesc, formulaLabel, formulaDesc, scaleXLabel, scaleXDesc, scaleYLabel, scaleYDesc, zoneLabel, zoneDesc);

        int insertIndex = 0;
        if (pc.statusLabel != null && pc.statusLabel.name != "PanelTitleLabel")
        {
            if (pc.statusLabel.gameObject.activeSelf)
            {
                pc.statusLabel.gameObject.SetActive(false);
                changed = true;
            }
        }

        insertIndex = PlaceBlock(content, titleLabel?.transform, insertIndex, ref changed);

        insertIndex = PlaceBlock(content, sectionFunctions?.transform, insertIndex, ref changed);

        // Cantidad
        insertIndex = PlaceBlock(content, countLabel?.transform, insertIndex, ref changed);
        insertIndex = PlaceBlock(content, pc.dropCount?.transform, insertIndex, ref changed);
        insertIndex = PlaceBlock(content, countDesc?.transform, insertIndex, ref changed);

        // Fórmula
        insertIndex = PlaceBlock(content, formulaLabel?.transform, insertIndex, ref changed);
        insertIndex = PlaceBlock(content, pc.dropFormula?.transform, insertIndex, ref changed);
        insertIndex = PlaceBlock(content, formulaDesc?.transform, insertIndex, ref changed);

        // Escala X
        insertIndex = PlaceBlock(content, scaleXLabel?.transform, insertIndex, ref changed);
        insertIndex = PlaceBlock(content, pc.inputScaleX?.transform, insertIndex, ref changed);
        insertIndex = PlaceBlock(content, scaleXDesc?.transform, insertIndex, ref changed);

        // Escala Y
        insertIndex = PlaceBlock(content, scaleYLabel?.transform, insertIndex, ref changed);
        insertIndex = PlaceBlock(content, pc.inputScaleY?.transform, insertIndex, ref changed);
        insertIndex = PlaceBlock(content, scaleYDesc?.transform, insertIndex, ref changed);

        // Zona
        insertIndex = PlaceBlock(content, zoneLabel?.transform, insertIndex, ref changed);
        insertIndex = PlaceBlock(content, pc.dropZone?.transform, insertIndex, ref changed);
        insertIndex = PlaceBlock(content, zoneDesc?.transform, insertIndex, ref changed);

        var spacer = content.Find("ButtonsSpacer");
        if (spacer == null)
        {
            var spacerGO = new GameObject("ButtonsSpacer", typeof(RectTransform), typeof(LayoutElement));
            spacerGO.transform.SetParent(content, false);
            spacer = spacerGO.transform;
            changed = true;
        }

        var spacerLE = spacer.GetComponent<LayoutElement>();
        if (spacerLE.preferredHeight != 8f)
        {
            spacerLE.preferredHeight = 8f;
            changed = true;
        }

        if (spacerLE.flexibleHeight != 0f)
        {
            spacerLE.flexibleHeight = 0f;
            changed = true;
        }

        if (spacer.GetSiblingIndex() != insertIndex)
        {
            spacer.SetSiblingIndex(insertIndex);
            changed = true;
        }

        var row = content.Find("ButtonsRow");
        if (row == null)
        {
            var rowGO = new GameObject("ButtonsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            rowGO.transform.SetParent(content, false);
            row = rowGO.transform;

            var rowLayout = rowGO.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 8f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;

            var rowLE = rowGO.GetComponent<LayoutElement>();
            rowLE.preferredHeight = 42f;
            rowLE.flexibleWidth = 1f;
            changed = true;
        }

        changed |= MoveButtonToRow(pc.btnGenerate, row);
        changed |= MoveButtonToRow(pc.btnReset, row);
        changed |= MoveButtonToRow(pc.btnDelete, row);
        EnsureGenerateButtonStyle(pc.btnGenerate, ref changed);

        if (row.GetSiblingIndex() != content.childCount - 1)
        {
            row.SetSiblingIndex(content.childCount - 1);
            changed = true;
        }

        if (content is RectTransform contentRt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);

        return changed;
    }

    static void EnsureDropdownVisualSafety(TMP_Dropdown dropdown, ref bool changed)
    {
        if (dropdown == null)
            return;

        if (dropdown.template != null && dropdown.template.gameObject.activeSelf)
        {
            dropdown.template.gameObject.SetActive(false);
            changed = true;
        }

        var rt = dropdown.GetComponent<RectTransform>();
        if (rt != null)
        {
            if (rt.anchorMin != new Vector2(0f, 0.5f) || rt.anchorMax != new Vector2(1f, 0.5f))
            {
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(1f, 0.5f);
                changed = true;
            }

            if (rt.sizeDelta.y < 36f)
            {
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, 36f);
                changed = true;
            }
        }

        var le = dropdown.GetComponent<LayoutElement>();
        if (le == null)
        {
            le = dropdown.gameObject.AddComponent<LayoutElement>();
            changed = true;
        }

        if (le.preferredHeight != 36f)
        {
            le.preferredHeight = 36f;
            changed = true;
        }

        if (le.flexibleWidth != 1f)
        {
            le.flexibleWidth = 1f;
            changed = true;
        }
    }

    static TextMeshProUGUI EnsureTitleLabel(Transform content, ref bool changed)
    {
        var title = FindDirectLabel(content, "PanelTitleLabel");
        if (title == null)
        {
            // Reuse a legacy label if available.
            var legacy = FindDirectLabel(content, "Lbl");
            if (legacy != null)
            {
                legacy.name = "PanelTitleLabel";
                title = legacy;
                changed = true;
            }
        }

        title = EnsureSimpleLabel(content, "PanelTitleLabel", "CAMPO VECTORIAL", 42f, FontStyles.Bold, ref changed, title);
        
        // Asegurar color azul claro brillante para el título
        if (title.color != new Color(0.08f, 0.56f, 1f))
        {
            title.color = new Color(0.08f, 0.56f, 1f);
            changed = true;
        }

        // Asegurar buen tamaño en el layout
        var le = title.GetComponent<LayoutElement>();
        if (le == null)
        {
            le = title.gameObject.AddComponent<LayoutElement>();
            changed = true;
        }

        if (le.preferredHeight != 48f)
        {
            le.preferredHeight = 48f;
            changed = true;
        }

        return title;
    }

    static TextMeshProUGUI EnsureDescriptionLabel(Transform content, ref bool changed)
    {
        var description = FindDirectLabel(content, "PanelDescriptionLabel");
        if (description == null)
        {
            var direct = content.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var candidate in direct)
            {
                if (candidate == null || candidate.transform.parent != content)
                    continue;

                var txt = candidate.text == null ? string.Empty : candidate.text.ToLowerInvariant();
                if (txt.Contains("ajusta") || txt.Contains("formula") || txt.Contains("zona"))
                {
                    candidate.name = "PanelDescriptionLabel";
                    description = candidate;
                    changed = true;
                    break;
                }
            }
        }

        description = EnsureSimpleLabel(
            content,
            "PanelDescriptionLabel",
            "Primero selecciona la cantidad y la formula. Luego ajusta multiplicadores y zona.",
            14f,
            FontStyles.Normal,
            ref changed,
            description);

        return description;
    }

    static TextMeshProUGUI EnsureLabelForDropdown(Transform content, TMP_Dropdown dropdown, string labelName, string labelText, ref bool changed)
    {
        if (dropdown == null)
            return null;

        var label = FindDirectLabel(content, labelName);
        if (label == null)
        {
            int targetIdx = Mathf.Max(dropdown.transform.GetSiblingIndex() - 1, 0);
            if (targetIdx < content.childCount)
            {
                var previous = content.GetChild(targetIdx);
                if (previous != null && previous.parent == content)
                {
                    var prevText = previous.GetComponent<TextMeshProUGUI>();
                    if (prevText != null)
                    {
                        prevText.name = labelName;
                        label = prevText;
                        changed = true;
                    }
                }
            }
        }

        label = EnsureSimpleLabel(content, labelName, labelText, 17f, FontStyles.Bold, ref changed, label);
        return label;
    }

    static TextMeshProUGUI EnsureLabelForInputField(Transform content, TMP_InputField inputField, string labelName, string labelText, ref bool changed)
    {
        if (inputField == null)
            return null;

        var label = FindDirectLabel(content, labelName);
        if (label == null)
        {
            int targetIdx = Mathf.Max(inputField.transform.GetSiblingIndex() - 1, 0);
            if (targetIdx < content.childCount)
            {
                var previous = content.GetChild(targetIdx);
                if (previous != null && previous.parent == content)
                {
                    var prevText = previous.GetComponent<TextMeshProUGUI>();
                    if (prevText != null)
                    {
                        prevText.name = labelName;
                        label = prevText;
                        changed = true;
                    }
                }
            }
        }

        label = EnsureSimpleLabel(content, labelName, labelText, 17f, FontStyles.Bold, ref changed, label);
        return label;
    }

    static TextMeshProUGUI EnsureSimpleLabel(
        Transform content,
        string name,
        string text,
        float fontSize,
        FontStyles style,
        ref bool changed,
        TextMeshProUGUI existing = null)
    {
        var label = existing != null ? existing : FindDirectLabel(content, name);
        if (label == null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(TextMeshProUGUI));
            go.transform.SetParent(content, false);
            label = go.GetComponent<TextMeshProUGUI>();
            changed = true;
        }

        if (label.name != name)
        {
            label.name = name;
            changed = true;
        }

        if (!label.gameObject.activeSelf)
        {
            label.gameObject.SetActive(true);
            changed = true;
        }

        if (label.text != text)
        {
            label.text = text;
            changed = true;
        }

        if (Mathf.Abs(label.fontSize - fontSize) > 0.01f)
        {
            label.fontSize = fontSize;
            changed = true;
        }

        if (label.fontStyle != style)
        {
            label.fontStyle = style;
            changed = true;
        }

        if (label.color != new Color(0.85f, 0.85f, 0.85f))
        {
            label.color = new Color(0.85f, 0.85f, 0.85f);
            changed = true;
        }

        if (label.textWrappingMode == TextWrappingModes.NoWrap)
        {
            label.textWrappingMode = TextWrappingModes.Normal;
            changed = true;
        }

        var le = label.GetComponent<LayoutElement>();
        if (le == null)
        {
            le = label.gameObject.AddComponent<LayoutElement>();
            changed = true;
        }

        float desiredHeight = fontSize >= 24f ? 44f : (fontSize >= 18f ? 28f : 20f);
        if (Mathf.Abs(le.preferredHeight - desiredHeight) > 0.01f)
        {
            le.preferredHeight = desiredHeight;
            changed = true;
        }

        if (le.flexibleWidth != 1f)
        {
            le.flexibleWidth = 1f;
            changed = true;
        }

        return label;
    }

    static TextMeshProUGUI FindDirectLabel(Transform content, string name)
    {
        var direct = content.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var txt in direct)
        {
            if (txt == null || txt.transform.parent != content)
                continue;

            if (txt.name == name)
                return txt;
        }

        return null;
    }

    static TextMeshProUGUI EnsureDescriptionSmall(Transform content, string name, string text, ref bool changed)
    {
        var label = FindDirectLabel(content, name);
        if (label == null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(TextMeshProUGUI));
            go.transform.SetParent(content, false);
            label = go.GetComponent<TextMeshProUGUI>();
            changed = true;
        }

        if (label.name != name)
        {
            label.name = name;
            changed = true;
        }

        if (label.text != text)
        {
            label.text = text;
            changed = true;
        }

        float fontSize = 12f;
        if (Mathf.Abs(label.fontSize - fontSize) > 0.01f)
        {
            label.fontSize = fontSize;
            changed = true;
        }

        // Color gris más tenue para descripciones
        Color descColor = new Color(0.65f, 0.65f, 0.65f, 0.9f);
        if (label.color != descColor)
        {
            label.color = descColor;
            changed = true;
        }

        if (label.fontStyle != FontStyles.Italic)
        {
            label.fontStyle = FontStyles.Italic;
            changed = true;
        }

        if (label.textWrappingMode == TextWrappingModes.NoWrap)
        {
            label.textWrappingMode = TextWrappingModes.Normal;
            changed = true;
        }

        var le = label.GetComponent<LayoutElement>();
        if (le == null)
        {
            le = label.gameObject.AddComponent<LayoutElement>();
            changed = true;
        }

        if (Mathf.Abs(le.preferredHeight - 12f) > 0.01f)
        {
            le.preferredHeight = 12f;
            changed = true;
        }

        if (le.flexibleWidth != 1f)
        {
            le.flexibleWidth = 1f;
            changed = true;
        }

        return label;
    }

    static bool DisableLegacyDuplicateLabels(Transform content, params TextMeshProUGUI[] keep)
    {
        var keepSet = new HashSet<Transform>();
        for (int i = 0; i < keep.Length; i++)
        {
            if (keep[i] != null)
                keepSet.Add(keep[i].transform);
        }

        bool changed = false;
        var direct = content.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var txt in direct)
        {
            if (txt == null || txt.transform.parent != content)
                continue;

            if (keepSet.Contains(txt.transform))
                continue;

            if (txt.name == "Lbl" || txt.name.Contains("Label") || txt.text.StartsWith("Formula"))
            {
                if (txt.gameObject.activeSelf)
                {
                    txt.gameObject.SetActive(false);
                    changed = true;
                }
            }
        }

        return changed;
    }

    static int PlaceLabeledDropdown(Transform content, TMP_Dropdown dropdown, string labelName, int index, ref bool changed)
    {
        var label = content.Find(labelName);
        index = PlaceBlock(content, label, index, ref changed);
        index = PlaceBlock(content, dropdown != null ? dropdown.transform : null, index, ref changed);
        return index;
    }

    static int PlaceBlock(Transform content, Transform target, int index, ref bool changed)
    {
        if (target == null || target.parent != content)
            return index;

        if (target.GetSiblingIndex() != index)
        {
            target.SetSiblingIndex(index);
            changed = true;
        }

        return index + 1;
    }

    static bool MoveButtonToRow(Button button, Transform row)
    {
        if (button == null || row == null)
            return false;

        bool changed = false;

        if (button.transform.parent != row)
        {
            button.transform.SetParent(row, false);
            changed = true;
        }

        var le = button.GetComponent<LayoutElement>();
        if (le == null)
        {
            le = button.gameObject.AddComponent<LayoutElement>();
            changed = true;
        }

        if (le.preferredHeight != 36f)
        {
            le.preferredHeight = 36f;
            changed = true;
        }

        if (le.flexibleWidth != 1f)
        {
            le.flexibleWidth = 1f;
            changed = true;
        }

        return changed;
    }

    static void EnsureGenerateButtonStyle(Button generateButton, ref bool changed)
    {
        if (generateButton == null)
            return;

        var text = generateButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
        {
            if (text.text != "Generar Campo Vectorial")
            {
                text.text = "Generar Campo Vectorial";
                changed = true;
            }

            if (text.color != Color.white)
            {
                text.color = Color.white;
                changed = true;
            }

            if (text.fontStyle != FontStyles.Bold)
            {
                text.fontStyle = FontStyles.Bold;
                changed = true;
            }
        }

        var image = generateButton.GetComponent<Image>();
        if (image != null)
        {
            Color normal = new Color(0.12f, 0.70f, 0.26f, 1f);
            if (image.color != normal)
            {
                image.color = normal;
                changed = true;
            }

            var colors = generateButton.colors;
            bool colorsChanged = false;

            if (colors.normalColor != normal)
            {
                colors.normalColor = normal;
                colorsChanged = true;
            }

            var highlighted = new Color(0.18f, 0.78f, 0.33f, 1f);
            if (colors.highlightedColor != highlighted)
            {
                colors.highlightedColor = highlighted;
                colorsChanged = true;
            }

            var pressed = new Color(0.08f, 0.58f, 0.20f, 1f);
            if (colors.pressedColor != pressed)
            {
                colors.pressedColor = pressed;
                colorsChanged = true;
            }

            if (colors.selectedColor != highlighted)
            {
                colors.selectedColor = highlighted;
                colorsChanged = true;
            }

            if (colors.colorMultiplier != 1f)
            {
                colors.colorMultiplier = 1f;
                colorsChanged = true;
            }

            if (colorsChanged)
            {
                generateButton.colors = colors;
                changed = true;
            }
        }
    }

    static TMP_Dropdown FindOrCreateDropdown(Transform parent, string objName, string labelText)
    {
        var existing = parent.Find(objName);
        if (existing != null)
        {
            var existingDrop = existing.GetComponent<TMP_Dropdown>();
            if (existingDrop != null)
                return existingDrop;
        }

        return CreateLabeledDropdown(parent, objName, labelText);
    }

    static TMP_InputField FindOrCreateInputField(Transform parent, string objName, string labelText)
    {
        var existing = parent.Find(objName);
        if (existing != null)
        {
            var existingInput = existing.GetComponent<TMP_InputField>();
            if (existingInput != null)
                return existingInput;
        }

        return CreateLabeledInputField(parent, objName, labelText);
    }

    static TMP_Dropdown CreateLabeledDropdown(Transform parent, string objName, string labelText)
    {
        // --- label ---
        GameObject labelGO = new GameObject(objName + "_Label");
        labelGO.transform.SetParent(parent, false);
        var rt = labelGO.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 24);

        var text = labelGO.AddComponent<TextMeshProUGUI>();
        text.text = labelText;
        text.fontSize = 18;
        text.color = new Color(0.85f, 0.85f, 0.85f);
        text.fontStyle = FontStyles.Bold;

        // --- dropdown ---
        // Clone the first TMP_Dropdown in the parent so structure + fonts match
        TMP_Dropdown source = parent.GetComponentInChildren<TMP_Dropdown>(true);
        GameObject dropGO = Object.Instantiate(source.gameObject, parent);
        dropGO.name = objName;

        // Remove any lingering options from the clone (InitDropdowns will fill them)
        TMP_Dropdown drop = dropGO.GetComponent<TMP_Dropdown>();
        drop.ClearOptions();

        return drop;
    }

    static TMP_InputField CreateLabeledInputField(Transform parent, string objName, string labelText)
    {
        // --- input field ---
        GameObject inputGO = new GameObject(objName);
        inputGO.transform.SetParent(parent, false);

        var inputRt = inputGO.AddComponent<RectTransform>();
        var inputLE = inputGO.AddComponent<LayoutElement>();
        inputLE.preferredHeight = 36f;
        inputLE.flexibleWidth = 1f;

        // Visual: Image as background
        var bgImage = inputGO.AddComponent<Image>();
        bgImage.color = new Color(0.10f, 0.20f, 0.30f, 0.9f);

        // Add border effect
        var outline = inputGO.AddComponent<Outline>();
        outline.effectColor = new Color(0.3f, 0.6f, 0.8f, 0.5f);
        outline.effectDistance = new Vector2(1, 1);

        // Text component
        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(inputGO.transform, false);
        var textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12, 0);
        textRect.offsetMax = new Vector2(-12, 0);

        var textMesh = textGO.AddComponent<TextMeshProUGUI>();
        textMesh.text = "1";
        textMesh.fontSize = 20;
        textMesh.color = new Color(0.9f, 0.95f, 1f);
        textMesh.alignment = TextAlignmentOptions.MidlineLeft;

        // Input field component
        var inputField = inputGO.AddComponent<TMP_InputField>();
        inputField.text = "1";
        inputField.characterLimit = 12;
        inputField.contentType = TMP_InputField.ContentType.DecimalNumber;
        inputField.textComponent = textMesh;
        inputField.targetGraphic = bgImage;

        return inputField;
    }

    static bool RemoveConflictingTrackedRaycasters(GameObject root)
    {
        int removedTotal = 0;
        string[] fullTypeNames =
        {
            "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit",
            "UnityEngine.InputSystem.UI.TrackedDeviceRaycaster, Unity.InputSystem",
        };

        foreach (var fullTypeName in fullTypeNames)
        {
            var type = System.Type.GetType(fullTypeName);
            if (type == null)
                continue;

            var components = root.GetComponentsInChildren(type, true);
            foreach (var component in components)
            {
                if (component is Component c)
                {
                    Object.DestroyImmediate(c);
                    removedTotal++;
                }
            }
        }

        if (removedTotal > 0)
        {
            Debug.Log($"[PanelSetup] Removed {removedTotal} conflicting tracked raycaster component(s).");
            return true;
        }

        return false;
    }
}
