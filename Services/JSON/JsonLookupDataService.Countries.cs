using System.Text.Json;

namespace MITANZ360Pro.Web.Services;

public partial class JsonLookupDataService
{
    // =====================================================
    // COUNTRY MODEL
    // =====================================================

    public class CountryModel
    {
        public string Name { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public string Currency { get; set; } = string.Empty;

        public string Timezone { get; set; } = string.Empty;
    }

    // =====================================================
    // FILE PATH
    // =====================================================

    private string CountriesFilePath =>
        Path.Combine(
            _environment.WebRootPath,
            "Data",
            "countries.json");

    // =====================================================
    // GET
    // =====================================================

    public async Task<List<CountryModel>>
        GetCountriesAsync()
    {
        return await LoadAsync<
            CountryModel>(
                "countries.json");
    }

    // =====================================================
    // SAVE FILE
    // =====================================================

    private async Task SaveCountriesAsync(
        List<CountryModel> countries)
    {
        var json =
            JsonSerializer.Serialize(
                countries,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

        await File.WriteAllTextAsync(
            CountriesFilePath,
            json);
    }

    // =====================================================
    // CREATE
    // =====================================================

    public async Task AddCountryAsync(
        CountryModel model)
    {
        var countries =
            await GetCountriesAsync();

        var exists =
            countries.Any(x =>
                x.Code.Equals(
                    model.Code,
                    StringComparison.OrdinalIgnoreCase));

        if (exists)
        {
            throw new InvalidOperationException(
                "Country code already exists.");
        }

        countries.Add(model);

        await SaveCountriesAsync(
            countries);
    }

    // =====================================================
    // UPDATE
    // =====================================================

    public async Task UpdateCountryAsync(
        CountryModel model)
    {
        var countries =
            await GetCountriesAsync();

        var existing =
            countries.FirstOrDefault(x =>
                x.Code.Equals(
                    model.Code,
                    StringComparison.OrdinalIgnoreCase));

        if (existing == null)
        {
            throw new InvalidOperationException(
                "Country not found.");
        }

        existing.Name =
            model.Name;

        existing.Currency =
            model.Currency;

        existing.Timezone =
            model.Timezone;

        await SaveCountriesAsync(
            countries);
    }

    // =====================================================
    // DELETE
    // =====================================================

    public async Task DeleteCountryAsync(
        string code)
    {
        var countries =
            await GetCountriesAsync();

        var existing =
            countries.FirstOrDefault(x =>
                x.Code.Equals(
                    code,
                    StringComparison.OrdinalIgnoreCase));

        if (existing == null)
            return;

        countries.Remove(existing);

        await SaveCountriesAsync(
            countries);
    }
}