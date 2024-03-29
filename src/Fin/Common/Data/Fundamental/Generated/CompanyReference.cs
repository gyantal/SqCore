//------------------------------------------------------------------------------
// This code was generated by a tool.
// Changes to this file may cause incorrect behavior and will be lost if
// the code is regenerated.
//------------------------------------------------------------------------------

// To get up to date fundamental definition files for your hedgefund contact sales@quantconnect.com

using System;
using Newtonsoft.Json;

namespace QuantConnect.Data.Fundamental
{
	/// <summary>
	/// Definition of the CompanyReference class
	/// </summary>
	public class CompanyReference
	{
		/// <summary>
		/// 10-digit unique and unchanging Morningstar identifier assigned to every company.
		/// </summary>
		/// <remarks>
		/// Morningstar DataId: 1
		/// </remarks>
		[JsonProperty("1")]
		public string CompanyId { get; set; }

		/// <summary>
		/// 25-character max abbreviated name of the firm.  In most cases, the short name will simply be the Legal Name less the
		/// "Corporation", "Corp.", "Inc.", "Incorporated", etc...
		/// </summary>
		/// <remarks>
		/// Morningstar DataId: 2
		/// </remarks>
		[JsonProperty("2")]
		public string ShortName { get; set; }

		/// <summary>
		/// The English translation of the foreign legal name if/when applicable.
		/// </summary>
		/// <remarks>
		/// Morningstar DataId: 3
		/// </remarks>
		[JsonProperty("3")]
		public string StandardName { get; set; }

		/// <summary>
		/// The full name of the registrant as specified in its charter, and most often found on the front cover of the 10K/10Q/20F filing.
		/// </summary>
		/// <remarks>
		/// Morningstar DataId: 4
		/// </remarks>
		[JsonProperty("4")]
		public string LegalName { get; set; }

		/// <summary>
		/// 3 Character ISO code of the country where the firm is domiciled. See separate reference document for Country Mappings.
		/// </summary>
		/// <remarks>
		/// Morningstar DataId: 5
		/// </remarks>
		[JsonProperty("5")]
		public string CountryId { get; set; }

		/// <summary>
		/// The Central Index Key; a corporate identifier assigned by the Securities and Exchange Commission (SEC).
		/// </summary>
		/// <remarks>
		/// Morningstar DataId: 6
		/// </remarks>
		[JsonProperty("6")]
		public string CIK { get; set; }

		/// <summary>
		/// At the Company level; each company is assigned to 1 of 3 possible status classifications; (U) Public, (V) Private, or (O) Obsolete:
		/// - Public-Firm is operating and currently has at least one common share class that is currently trading on a public exchange.
		/// - Private-Firm is operating but does not have any common share classes currently trading on a public exchange.
		/// - Obsolete-Firm is no longer operating because it closed its business, or was acquired.
		/// </summary>
		/// <remarks>
		/// Morningstar DataId: 9
		/// </remarks>
		[JsonProperty("9")]
		public string CompanyStatus { get; set; }

		/// <summary>
		/// The Month of the company's latest fiscal year.
		/// </summary>
		/// <remarks>
		/// Morningstar DataId: 10
		/// </remarks>
		[JsonProperty("10")]
		public int FiscalYearEnd { get; set; }

		/// <summary>
		/// This indicator will denote which one of the six industry data collection templates applies to the company.  Each industry data
		/// collection template includes data elements that are commonly reported by companies in that industry.  N=Normal
		/// (Manufacturing), M=Mining,  U=Utility, T=Transportation, B=Bank, I=Insurance
		/// </summary>
		/// <remarks>
		/// Morningstar DataId: 11
		/// </remarks>
		[JsonProperty("11")]
		public string IndustryTemplateCode { get; set; }

		/// <summary>
		/// The 10-digit unique and unchanging Morningstar identifier assigned to the Primary Share class of a company. The primary share of a
		/// company is defined as the first share that was traded publicly and is still actively trading. If this share is no longer trading, the
		/// primary share will be the share with the highest volume.
		/// </summary>
		/// <remarks>
		/// Morningstar DataId: 12
		/// </remarks>
		[JsonProperty("12")]
		public string PrimaryShareClassID { get; set; }

		/// <summary>
		/// The symbol of the Primary Share of the company, composed of an arrangement of characters (often letters) representing a
		/// particular security listed on an exchange or otherwise traded publicly.   The primary share of a company is defined as the first share
		/// that was traded publicly and is still actively trading. If this share is no longer trading, the primary share will be the share with the
		/// highest volume. Note: Morningstar's multi-share class symbols will often contain a "period" within the symbol; e.g. BRK.B for
		/// Berkshire Hathaway Class B.
		/// </summary>
		/// <remarks>
		/// Morningstar DataId: 13
		/// </remarks>
		[JsonProperty("13")]
		public string PrimarySymbol { get; set; }

		/// <summary>
		/// The Id representing the stock exchange of the Primary Share of the company.  See separate reference document for Exchange
		/// Mappings. The primary share of a company is defined as the first share that was traded publicly with and is still actively trading. If
		/// this share is no longer trading, the primary share will be the share with the highest volume.
		/// </summary>
		/// <remarks>
		/// Morningstar DataId: 14
		/// </remarks>
		[JsonProperty("14")]
		public string PrimaryExchangeID { get; set; }

		/// <summary>
		/// In some cases, different from the country of domicile (CountryId; DataID 5).  This element is a three (3) Character ISO code of the
		/// business country of the security.  It is determined by a few factors, including:
		/// </summary>
		/// <remarks>
		/// Morningstar DataId: 15
		/// </remarks>
		[JsonProperty("15")]
		public string BusinessCountryID { get; set; }

		/// <summary>
		/// The language code for the foreign legal name if/when applicable.  Related to  DataID 4 (LegalName).
		/// </summary>
		/// <remarks>
		/// Morningstar DataId: 16
		/// </remarks>
		[JsonProperty("16")]
		public string LegalNameLanguageCode { get; set; }

		/// <summary>
		/// The legal (registered) name of the company's current auditor. Distinct from DataID 28000 Period Auditor that identifies the Auditor
		/// related to that period's financial statements.
		/// </summary>
		/// <remarks>
		/// Morningstar DataId: 17
		/// </remarks>
		[JsonProperty("17")]
		public string Auditor { get; set; }

		/// <summary>
		/// The ISO code denoting the language text for Auditor's name and contact information.
		/// </summary>
		/// <remarks>
		/// Morningstar DataId: 18
		/// </remarks>
		[JsonProperty("18")]
		public string AuditorLanguageCode { get; set; }

		/// <summary>
		/// The legal (registered) name of the current legal Advisor of the company.
		/// </summary>
		/// <remarks>
		/// Morningstar DataId: 19
		/// </remarks>
		[JsonProperty("19")]
		public string Advisor { get; set; }

		/// <summary>
		/// The ISO code denoting the language text for Advisor's name and contact information.
		/// </summary>
		/// <remarks>
		/// Morningstar DataId: 20
		/// </remarks>
		[JsonProperty("20")]
		public string AdvisorLanguageCode { get; set; }

		/// <summary>
		/// Indicator to denote if the company is a limited partnership, which is a form of business structure comprised of a general partner and
		/// limited partners. 1 denotes it is a LP; otherwise 0.
		/// </summary>
		/// <remarks>
		/// Morningstar DataId: 21
		/// </remarks>
		[JsonProperty("21")]
		public bool IsLimitedPartnership { get; set; }

		/// <summary>
		/// Indicator to denote if the company is a real estate investment trust (REIT). 1 denotes it is a REIT; otherwise 0.
		/// </summary>
		/// <remarks>
		/// Morningstar DataId: 22
		/// </remarks>
		[JsonProperty("22")]
		public bool IsREIT { get; set; }

		/// <summary>
		/// The MIC (market identifier code) of the PrimarySymbol of the company. See Data Appendix A for the relevant MIC to exchange
		/// name mapping.
		/// </summary>
		/// <remarks>
		/// Morningstar DataId: 23
		/// </remarks>
		[JsonProperty("23")]
		public string PrimaryMIC { get; set; }

		/// <summary>
		/// This refers to the financial template used to collect the company's financial statements. There are two report styles representing
		/// two different financial template structures. Report style "1" is most commonly used by US and Canadian companies, and Report
		/// style "3" is most commonly used by the rest of the universe. Contact your client manager for access to the respective templates.
		/// </summary>
		/// <remarks>
		/// Morningstar DataId: 24
		/// </remarks>
		[JsonProperty("24")]
		public int ReportStyle { get; set; }

		/// <summary>
		/// The year a company was founded.
		/// </summary>
		/// <remarks>
		/// Morningstar DataId: 25
		/// </remarks>
		[JsonProperty("25")]
		public string YearofEstablishment { get; set; }

		/// <summary>
		/// Indicator to denote if the company is a limited liability company. 1 denotes it is a LLC; otherwise 0.
		/// </summary>
		/// <remarks>
		/// Morningstar DataId: 26
		/// </remarks>
		[JsonProperty("26")]
		public bool IsLimitedLiabilityCompany { get; set; }

		/// <summary>
		/// The upcoming expected year end for the company. It is calculated based on current year end (from latest available annual report)
		/// + 1 year.
		/// </summary>
		/// <remarks>
		/// Morningstar DataId: 27
		/// </remarks>
		[JsonProperty("27")]
		public DateTime ExpectedFiscalYearEnd { get; set; }

		/// <summary>
		/// Creates an instance of the CompanyReference class
		/// </summary>
		public CompanyReference()
		{
		}

		/// <summary>
		/// Applies updated values from <paramref name="update"/> to this instance
		/// </summary>
		/// <remarks>Used to apply data updates to the current instance. This WILL overwrite existing values. Default update values are ignored.</remarks>
		/// <param name="update">The next data update for this instance</param>
		public void UpdateValues(CompanyReference update)
		{
			if (update == null) return;

			if (!string.IsNullOrWhiteSpace(update.CompanyId)) CompanyId = update.CompanyId;
			if (!string.IsNullOrWhiteSpace(update.ShortName)) ShortName = update.ShortName;
			if (!string.IsNullOrWhiteSpace(update.StandardName)) StandardName = update.StandardName;
			if (!string.IsNullOrWhiteSpace(update.LegalName)) LegalName = update.LegalName;
			if (!string.IsNullOrWhiteSpace(update.CountryId)) CountryId = update.CountryId;
			if (!string.IsNullOrWhiteSpace(update.CIK)) CIK = update.CIK;
			if (!string.IsNullOrWhiteSpace(update.CompanyStatus)) CompanyStatus = update.CompanyStatus;
			if (update.FiscalYearEnd != default(int)) FiscalYearEnd = update.FiscalYearEnd;
			if (!string.IsNullOrWhiteSpace(update.IndustryTemplateCode)) IndustryTemplateCode = update.IndustryTemplateCode;
			if (!string.IsNullOrWhiteSpace(update.PrimaryShareClassID)) PrimaryShareClassID = update.PrimaryShareClassID;
			if (!string.IsNullOrWhiteSpace(update.PrimarySymbol)) PrimarySymbol = update.PrimarySymbol;
			if (!string.IsNullOrWhiteSpace(update.PrimaryExchangeID)) PrimaryExchangeID = update.PrimaryExchangeID;
			if (!string.IsNullOrWhiteSpace(update.BusinessCountryID)) BusinessCountryID = update.BusinessCountryID;
			if (!string.IsNullOrWhiteSpace(update.LegalNameLanguageCode)) LegalNameLanguageCode = update.LegalNameLanguageCode;
			if (!string.IsNullOrWhiteSpace(update.Auditor)) Auditor = update.Auditor;
			if (!string.IsNullOrWhiteSpace(update.AuditorLanguageCode)) AuditorLanguageCode = update.AuditorLanguageCode;
			if (!string.IsNullOrWhiteSpace(update.Advisor)) Advisor = update.Advisor;
			if (!string.IsNullOrWhiteSpace(update.AdvisorLanguageCode)) AdvisorLanguageCode = update.AdvisorLanguageCode;
			if (update.IsLimitedPartnership != default(bool)) IsLimitedPartnership = update.IsLimitedPartnership;
			if (update.IsREIT != default(bool)) IsREIT = update.IsREIT;
			if (!string.IsNullOrWhiteSpace(update.PrimaryMIC)) PrimaryMIC = update.PrimaryMIC;
			if (update.ReportStyle != default(int)) ReportStyle = update.ReportStyle;
			if (!string.IsNullOrWhiteSpace(update.YearofEstablishment)) YearofEstablishment = update.YearofEstablishment;
			if (update.IsLimitedLiabilityCompany != default(bool)) IsLimitedLiabilityCompany = update.IsLimitedLiabilityCompany;
			if (update.ExpectedFiscalYearEnd != default(DateTime)) ExpectedFiscalYearEnd = update.ExpectedFiscalYearEnd;
		}
	}
}
