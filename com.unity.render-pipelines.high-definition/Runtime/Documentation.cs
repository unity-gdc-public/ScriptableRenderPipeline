namespace UnityEngine.Rendering.HighDefinition
{
    //Need to live in Runtime as Attribute of documentation is on Runtime classes \o/
    class Documentation
    {
        //This must be used like
        //[HelpURL(Documentation.baseURL + Documentation.version + Documentation.subURL + "some-page" + Documentation.endURL)]
        //It cannot support String.Format nor string interpolation
        internal const string baseURL = "https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@";
        internal const string subURL = "/manual/";
        internal const string endURL = ".html";

        //Update this field when upgrading the target Documentation for the package
        //Should be linked to the package version somehow.
        internal const string version = "6.9";
    }
}
