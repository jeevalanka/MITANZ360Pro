namespace MITANZ360Pro.Web.Models;

public class NavigationCardModel
{
    // =====================================================
    // SYSTEM
    // =====================================================
    public string Id
    {
        get;
        set;
    } = string.Empty;

    // =====================================================
    // BASIC
    // =====================================================
    public string Title
    {
        get;
        set;
    } = string.Empty;
    public string Subtitle
    {
        get;
        set;
    } = string.Empty;

    // =====================================================
    // VISUALS
    // =====================================================
    public string Icon
    {
        get;
        set;
    } = string.Empty;
    public string ImageUrl
    {
        get;
        set;
    } = string.Empty;
    public string CardColor
    {
        get;
        set;
    } = string.Empty;

    // =====================================================
    // NAVIGATION
    // =====================================================
    public string Url
    {
        get;
        set;
    } = string.Empty;

    public string OpenType
    {
        get;
        set;
    } = string.Empty;

    // =====================================================
    // FILTERS
    // =====================================================
    public string Role
    {
        get;
        set;
    } = string.Empty;

    public string Campus
    {
        get;
        set;
    } = string.Empty;

    // =====================================================
    // SETTINGS
    // =====================================================
    public bool IsEnabled
    {
        get;
        set;
    }
    public int SortOrder
    {
        get;
        set;
    }

    // =====================================================
    // AUDIT
    // =====================================================

    public DateTime Created
    {
        get;
        set;
    }
    public DateTime Modified
    {
        get;
        set;
    }
    public string CreatedBy
    {
        get;
        set;
    } = string.Empty;

    public string ModifiedBy
    {
        get;
        set;
    } = string.Empty;
}