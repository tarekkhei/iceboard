/// <summary>
/// Parameterized Oracle SQL for Ice Wireless dashboard.
/// BUSINESS RULES (mandatory):
/// - New Active Lines: FROM accounts a, COUNT(DISTINCT a.id), ACCOUNTS.REGISTRATIONDATE only.
/// - Current Active Lines: ACCOUNTSERVICES.OWNSTATUS = 'A', no date filter.
/// - Status events: ACCTSTATUS.FROMDATE is the event date. TODATE is status end only.
/// - Never use ACTIVATIONDATE / FIRSTACTIVATIONDATE for New Active Lines.
/// - Product ICENP_MASTER is always excluded from dashboard queries.
/// </summary>
namespace Icewireless.AccountServiceDashboard.Infrastructure.Oracle;

public static class OracleSql
{
    public const string ProviderPredicate = "a.providercode = :p_provider";

    /// <summary>Product code permanently excluded from KPIs, charts, details, and exports.</summary>
    public const string ExcludedProductCode = "ICENP_MASTER";

    /// <summary>Catalog name from PRODUCTS.DESCRIPTION; falls back to ACCOUNTSERVICES.PRODUCTCODE.</summary>
    public const string ProductDisplayExpr = "NVL(p.description, acs.productcode)";

    /// <summary>
    /// English status-reason label. STATREASON.DESCR is LANG_TRANSLATIONS.NUMERIC_ID;
    /// TRANSLATED_STRING is the UI text. Falls back to STATREASON.DESCRIPTION, then the numeric code.
    /// </summary>
    public const string ReasonDisplayExpr = "NVL(lt.translated_string, NVL(sr.description, TO_CHAR(ast.reason)))";

    public const string ReasonChartLabelExpr = "NVL(lt.translated_string, NVL(sr.description, NVL(TO_CHAR(ast.reason), 'Unknown')))";

    public const string ReasonLookupJoins = @"
LEFT JOIN statreason sr ON sr.reasonnum = ast.reason
LEFT JOIN lang_translations lt ON lt.numeric_id = TO_NUMBER(sr.descr) AND LOWER(lt.iso3code) = 'eng'";

    // Account-centric foundation. Supporting tables only via EXISTS for filters.
    public static readonly string NewActiveBaseFrom = AccountExclusionQueryBuilder.Apply(
        @"
FROM accounts a
WHERE a.providercode = :p_provider
  AND a.registrationdate >= :p_from
  AND a.registrationdate < :p_to_exclusive
  AND (
        :p_region IS NULL
        OR EXISTS (
            SELECT 1
            FROM acctcontacts c
            WHERE c.accountid = a.id
              AND UPPER(TRIM(c.state)) = UPPER(TRIM(:p_region))
        )
      )
  AND (
        :p_product IS NULL
        OR EXISTS (
            SELECT 1
            FROM accountservices acs
            WHERE acs.accountid = a.id
              AND acs.productcode = :p_product
        )
      )
AND (
      :p_exclude_product IS NULL
      OR NOT EXISTS (
          SELECT 1 FROM accountservices ep
          WHERE ep.accountid = a.id AND ep.productcode = :p_exclude_product
      )
)
  AND (
        :p_service_type IS NULL
        OR EXISTS (
            SELECT 1
            FROM accountservices acs
            WHERE acs.accountid = a.id
              AND acs.servicetype = :p_service_type
        )
      )
  AND (
        :p_search IS NULL
        OR UPPER(a.code) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
        OR UPPER(a.accountname) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
        OR EXISTS (
            SELECT 1
            FROM accountservices acs
            LEFT JOIN products p ON p.code = acs.productcode
            WHERE acs.accountid = a.id
              AND (
                    UPPER(acs.servicecode) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
                    OR UPPER(acs.productcode) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
                    OR UPPER(p.description) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
                  )
        )
      )");

    public static readonly string CountNewActiveLines = @"
SELECT COUNT(DISTINCT a.id) AS CNT
" + NewActiveBaseFrom;

    // One row per account (account-centric). Supporting service/contact via ROW_NUMBER.
    public static readonly string SelectNewActiveLines = AccountExclusionQueryBuilder.Apply(
        @"
SELECT * FROM (
  SELECT inner_q.*, ROWNUM AS rnum FROM (
    SELECT
      service_id, registration_date, account_code, account_name, product, service_code,
      service_type, quantity, customer, region, email
    FROM (
      SELECT
        NVL(acs.id, 0) AS service_id,
        a.registrationdate AS registration_date,
        a.code AS account_code,
        a.accountname AS account_name,
        NVL(p.description, acs.productcode) AS product,
        acs.servicecode AS service_code,
        acs.servicetype AS service_type,
        aq.quantity AS quantity,
        TRIM(NVL(c.firstname,'') || ' ' || NVL(c.middlename,'') || ' ' || NVL(c.lastname,'')) AS customer,
        c.state AS region,
        c.email AS email,
        ROW_NUMBER() OVER (
          PARTITION BY a.id
          ORDER BY acs.id NULLS LAST, aq.fromdate DESC NULLS LAST, c.email NULLS LAST) AS rn
      FROM accounts a
      LEFT JOIN accountservices acs ON acs.accountid = a.id
      LEFT JOIN products p ON p.code = acs.productcode
      LEFT JOIN asquantity aq ON aq.asid = acs.id
      LEFT JOIN acctcontacts c ON c.accountid = a.id
      WHERE a.providercode = :p_provider
        AND a.registrationdate >= :p_from
        AND a.registrationdate < :p_to_exclusive
        AND (
              :p_region IS NULL
              OR EXISTS (
                  SELECT 1 FROM acctcontacts cx
                  WHERE cx.accountid = a.id
                    AND UPPER(TRIM(cx.state)) = UPPER(TRIM(:p_region))
              )
            )
        AND (
              :p_product IS NULL
              OR EXISTS (
                  SELECT 1 FROM accountservices sx
                  WHERE sx.accountid = a.id AND sx.productcode = :p_product
              )
            )
AND (
      :p_exclude_product IS NULL
      OR NOT EXISTS (
          SELECT 1 FROM accountservices ep
          WHERE ep.accountid = a.id AND ep.productcode = :p_exclude_product
      )
)
        AND (
              :p_service_type IS NULL
              OR EXISTS (
                  SELECT 1 FROM accountservices sx
                  WHERE sx.accountid = a.id AND sx.servicetype = :p_service_type
              )
            )
        AND (
              :p_search IS NULL
              OR UPPER(a.code) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
              OR UPPER(a.accountname) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
              OR EXISTS (
                  SELECT 1 FROM accountservices sx
                  LEFT JOIN products px ON px.code = sx.productcode
                  WHERE sx.accountid = a.id
                    AND (
                          UPPER(sx.servicecode) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
                          OR UPPER(sx.productcode) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
                          OR UPPER(px.description) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
                        )
              )
            )
    ) ranked
    WHERE ranked.rn = 1
    ORDER BY registration_date DESC, account_code
  ) inner_q
  WHERE ROWNUM <= :p_offset + :p_page_size
)
WHERE rnum > :p_offset");

    // Account-centric current snapshot. No registration/activation date range on this KPI.
    public static readonly string CurrentActiveBaseFrom = AccountExclusionQueryBuilder.Apply(
        @"
FROM accounts a
WHERE a.providercode = :p_provider
  AND a.activationdate IS NOT NULL
  AND a.ownstatus = :p_active_status
  AND (
        :p_region IS NULL
        OR EXISTS (
            SELECT 1 FROM acctcontacts c
            WHERE c.accountid = a.id
              AND UPPER(TRIM(c.state)) = UPPER(TRIM(:p_region))
        )
      )
  AND (
        :p_product IS NULL
        OR EXISTS (
            SELECT 1 FROM accountservices acs
            WHERE acs.accountid = a.id
              AND acs.productcode = :p_product
        )
      )
AND (
      :p_exclude_product IS NULL
      OR NOT EXISTS (
          SELECT 1 FROM accountservices ep
          WHERE ep.accountid = a.id AND ep.productcode = :p_exclude_product
      )
)
  AND (
        :p_service_type IS NULL
        OR EXISTS (
            SELECT 1 FROM accountservices acs
            WHERE acs.accountid = a.id
              AND acs.servicetype = :p_service_type
        )
      )
  AND (
        :p_search IS NULL
        OR UPPER(a.code) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
        OR UPPER(a.accountname) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
        OR EXISTS (
            SELECT 1 FROM accountservices acs
            LEFT JOIN products p ON p.code = acs.productcode
            WHERE acs.accountid = a.id
              AND (
                    UPPER(acs.servicecode) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
                    OR UPPER(acs.productcode) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
                    OR UPPER(p.description) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
                  )
        )
      )");

    public static readonly string CountCurrentActiveLines = @"
SELECT COUNT(DISTINCT a.id) AS CNT
" + CurrentActiveBaseFrom;

    // One row per currently active account.
    public static readonly string SelectCurrentActiveLines = AccountExclusionQueryBuilder.Apply(
        @"
SELECT * FROM (
  SELECT inner_q.*, ROWNUM AS rnum FROM (
    SELECT
      account_id, account_code, account_name, status_code,
      customer, region, email, activation_date, product, service_code, service_type
    FROM (
      SELECT
        a.id AS account_id,
        a.code AS account_code,
        a.accountname AS account_name,
        a.ownstatus AS status_code,
        TRIM(NVL(c.firstname,'') || ' ' || NVL(c.middlename,'') || ' ' || NVL(c.lastname,'')) AS customer,
        c.state AS region,
        c.email AS email,
        a.activationdate AS activation_date,
        NVL(p.description, acs.productcode) AS product,
        acs.servicecode AS service_code,
        acs.servicetype AS service_type,
        ROW_NUMBER() OVER (
          PARTITION BY a.id
          ORDER BY acs.id NULLS LAST, c.email NULLS LAST) AS rn
      FROM accounts a
      LEFT JOIN accountservices acs ON acs.accountid = a.id
      LEFT JOIN products p ON p.code = acs.productcode
      LEFT JOIN acctcontacts c ON c.accountid = a.id
      WHERE a.providercode = :p_provider
        AND a.activationdate IS NOT NULL
        AND a.ownstatus = :p_active_status
        AND (
              :p_region IS NULL
              OR EXISTS (
                  SELECT 1 FROM acctcontacts cx
                  WHERE cx.accountid = a.id
                    AND UPPER(TRIM(cx.state)) = UPPER(TRIM(:p_region))
              )
            )
        AND (
              :p_product IS NULL
              OR EXISTS (
                  SELECT 1 FROM accountservices sx
                  WHERE sx.accountid = a.id AND sx.productcode = :p_product
              )
            )
AND (
      :p_exclude_product IS NULL
      OR NOT EXISTS (
          SELECT 1 FROM accountservices ep
          WHERE ep.accountid = a.id AND ep.productcode = :p_exclude_product
      )
)
        AND (
              :p_service_type IS NULL
              OR EXISTS (
                  SELECT 1 FROM accountservices sx
                  WHERE sx.accountid = a.id AND sx.servicetype = :p_service_type
              )
            )
        AND (
              :p_search IS NULL
              OR UPPER(a.code) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
              OR UPPER(a.accountname) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
              OR EXISTS (
                  SELECT 1 FROM accountservices sx
                  LEFT JOIN products px ON px.code = sx.productcode
                  WHERE sx.accountid = a.id
                    AND (
                          UPPER(sx.servicecode) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
                          OR UPPER(sx.productcode) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
                          OR UPPER(px.description) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
                        )
              )
            )
    ) ranked
    WHERE ranked.rn = 1
    ORDER BY account_name, account_id
  ) inner_q
  WHERE ROWNUM <= :p_offset + :p_page_size
)
WHERE rnum > :p_offset");

    // Activated during selected period — separate KPI from current snapshot.
    public static readonly string ActivatedDuringPeriodBaseFrom = AccountExclusionQueryBuilder.Apply(
        @"
FROM accounts a
WHERE a.providercode = :p_provider
  AND a.activationdate IS NOT NULL
  AND a.activationdate >= :p_from
  AND a.activationdate < :p_to_exclusive
  AND (
        :p_region IS NULL
        OR EXISTS (
            SELECT 1 FROM acctcontacts c
            WHERE c.accountid = a.id
              AND UPPER(TRIM(c.state)) = UPPER(TRIM(:p_region))
        )
      )
  AND (
        :p_product IS NULL
        OR EXISTS (
            SELECT 1 FROM accountservices acs
            WHERE acs.accountid = a.id
              AND acs.productcode = :p_product
        )
      )
AND (
      :p_exclude_product IS NULL
      OR NOT EXISTS (
          SELECT 1 FROM accountservices ep
          WHERE ep.accountid = a.id AND ep.productcode = :p_exclude_product
      )
)
  AND (
        :p_service_type IS NULL
        OR EXISTS (
            SELECT 1 FROM accountservices acs
            WHERE acs.accountid = a.id
              AND acs.servicetype = :p_service_type
        )
      )
  AND (
        :p_search IS NULL
        OR UPPER(a.code) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
        OR UPPER(a.accountname) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
        OR EXISTS (
            SELECT 1 FROM accountservices acs
            LEFT JOIN products p ON p.code = acs.productcode
            WHERE acs.accountid = a.id
              AND (
                    UPPER(acs.servicecode) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
                    OR UPPER(acs.productcode) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
                    OR UPPER(p.description) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
                  )
        )
      )");

    public static readonly string CountActivatedDuringPeriod = @"
SELECT COUNT(DISTINCT a.id) AS CNT
" + ActivatedDuringPeriodBaseFrom;

    public static readonly string SelectActivatedDuringPeriod = AccountExclusionQueryBuilder.Apply(
        @"
SELECT * FROM (
  SELECT inner_q.*, ROWNUM AS rnum FROM (
    SELECT
      account_id, account_code, account_name, status_code,
      customer, region, email, activation_date, product, service_code, service_type
    FROM (
      SELECT
        a.id AS account_id,
        a.code AS account_code,
        a.accountname AS account_name,
        a.ownstatus AS status_code,
        TRIM(NVL(c.firstname,'') || ' ' || NVL(c.middlename,'') || ' ' || NVL(c.lastname,'')) AS customer,
        c.state AS region,
        c.email AS email,
        a.activationdate AS activation_date,
        NVL(p.description, acs.productcode) AS product,
        acs.servicecode AS service_code,
        acs.servicetype AS service_type,
        ROW_NUMBER() OVER (
          PARTITION BY a.id
          ORDER BY acs.id NULLS LAST, c.email NULLS LAST) AS rn
      FROM accounts a
      LEFT JOIN accountservices acs ON acs.accountid = a.id
      LEFT JOIN products p ON p.code = acs.productcode
      LEFT JOIN acctcontacts c ON c.accountid = a.id
      WHERE a.providercode = :p_provider
        AND a.activationdate IS NOT NULL
        AND a.activationdate >= :p_from
        AND a.activationdate < :p_to_exclusive
        AND (
              :p_region IS NULL
              OR EXISTS (
                  SELECT 1 FROM acctcontacts cx
                  WHERE cx.accountid = a.id
                    AND UPPER(TRIM(cx.state)) = UPPER(TRIM(:p_region))
              )
            )
        AND (
              :p_product IS NULL
              OR EXISTS (
                  SELECT 1 FROM accountservices sx
                  WHERE sx.accountid = a.id AND sx.productcode = :p_product
              )
            )
AND (
      :p_exclude_product IS NULL
      OR NOT EXISTS (
          SELECT 1 FROM accountservices ep
          WHERE ep.accountid = a.id AND ep.productcode = :p_exclude_product
      )
)
        AND (
              :p_service_type IS NULL
              OR EXISTS (
                  SELECT 1 FROM accountservices sx
                  WHERE sx.accountid = a.id AND sx.servicetype = :p_service_type
              )
            )
        AND (
              :p_search IS NULL
              OR UPPER(a.code) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
              OR UPPER(a.accountname) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
              OR EXISTS (
                  SELECT 1 FROM accountservices sx
                  LEFT JOIN products px ON px.code = sx.productcode
                  WHERE sx.accountid = a.id
                    AND (
                          UPPER(sx.servicecode) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
                          OR UPPER(sx.productcode) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
                          OR UPPER(px.description) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
                        )
              )
            )
    ) ranked
    WHERE ranked.rn = 1
    ORDER BY activation_date DESC, account_name
  ) inner_q
  WHERE ROWNUM <= :p_offset + :p_page_size
)
WHERE rnum > :p_offset");

    // Opening active for churn: accounts with Active status covering period start.
    // TODATE here is status coverage end, not the event date.
    public static readonly string CountOpeningActiveLines = AccountExclusionQueryBuilder.Apply(
        @"
SELECT COUNT(DISTINCT a.id) AS CNT
FROM accounts a
INNER JOIN acctstatus ast ON ast.accountid = a.id
WHERE a.providercode = :p_provider
  AND a.activationdate IS NOT NULL
  AND ast.status = :p_active_status
  AND ast.fromdate <= :p_from
  AND (ast.todate IS NULL OR ast.todate >= :p_from)
  AND (
        :p_region IS NULL
        OR EXISTS (
            SELECT 1 FROM acctcontacts cx
            WHERE cx.accountid = a.id AND UPPER(TRIM(cx.state)) = UPPER(TRIM(:p_region))
        )
      )
  AND (
        :p_product IS NULL
        OR EXISTS (
            SELECT 1 FROM accountservices acs
            WHERE acs.accountid = a.id AND acs.productcode = :p_product
        )
      )
AND (
      :p_exclude_product IS NULL
      OR NOT EXISTS (
          SELECT 1 FROM accountservices ep
          WHERE ep.accountid = a.id AND ep.productcode = :p_exclude_product
      )
)
  AND (
        :p_service_type IS NULL
        OR EXISTS (
            SELECT 1 FROM accountservices acs
            WHERE acs.accountid = a.id AND acs.servicetype = :p_service_type
        )
      )");

    // Suspended Events KPI — unique accounts that entered Suspended (S) during the selected period.
    // ACCTSTATUS.FROMDATE is the event date. TODATE is status end only and is not used to filter.
    public static readonly string SuspendedAccountsBaseFrom = AccountExclusionQueryBuilder.Apply(
        @"
FROM accounts a
WHERE a.providercode = :p_provider
  AND EXISTS (
        SELECT 1
        FROM acctstatus ast
        WHERE ast.accountid = a.id
          AND ast.status = :p_status
          AND ast.fromdate >= :p_from
          AND ast.fromdate < :p_to_exclusive
      )
  AND (
        :p_region IS NULL
        OR EXISTS (
            SELECT 1 FROM acctcontacts c
            WHERE c.accountid = a.id
              AND UPPER(TRIM(c.state)) = UPPER(TRIM(:p_region))
        )
      )
  AND (
        :p_product IS NULL
        OR EXISTS (
            SELECT 1 FROM accountservices acs
            WHERE acs.accountid = a.id
              AND acs.productcode = :p_product
        )
      )
AND (
      :p_exclude_product IS NULL
      OR NOT EXISTS (
          SELECT 1 FROM accountservices ep
          WHERE ep.accountid = a.id AND ep.productcode = :p_exclude_product
      )
)
  AND (
        :p_service_type IS NULL
        OR EXISTS (
            SELECT 1 FROM accountservices acs
            WHERE acs.accountid = a.id
              AND acs.servicetype = :p_service_type
        )
      )
  AND (
        :p_search IS NULL
        OR UPPER(a.code) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
        OR UPPER(a.accountname) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
        OR EXISTS (
            SELECT 1 FROM accountservices acs
            LEFT JOIN products p ON p.code = acs.productcode
            WHERE acs.accountid = a.id
              AND (
                    UPPER(acs.servicecode) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
                    OR UPPER(acs.productcode) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
                    OR UPPER(p.description) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
                  )
        )
      )");

    public static readonly string CountSuspendedAccounts = @"
SELECT COUNT(DISTINCT a.id) AS CNT
" + SuspendedAccountsBaseFrom;

    // Pending Cancellation KPI — unique accounts that entered Pending to Close (C) during the selected period.
    // ACCTSTATUS.FROMDATE is the event date. Same FROMDATE-entry pattern as Suspended.
    public static readonly string PendingCancellationAccountsBaseFrom = SuspendedAccountsBaseFrom;

    public static readonly string CountPendingCancellationAccounts = @"
SELECT COUNT(DISTINCT a.id) AS CNT
" + PendingCancellationAccountsBaseFrom;

    // One row per account that entered Pending to Close (C) during the period (FROMDATE event).
    public static readonly string SelectPendingCancellationAccounts = AccountExclusionQueryBuilder.Apply(
        @"
SELECT * FROM (
  SELECT inner_q.*, ROWNUM AS rnum FROM (
    SELECT
      a.id AS account_id,
      CAST(NULL AS NUMBER) AS service_id,
      a.code AS account_code,
      a.accountname AS account_name,
      :p_status AS status_code,
      qpc.fromdate AS status_from_date,
      qpc.todate AS status_end_date,
      qpc.reason AS reason,
      (
        SELECT MIN(NVL(p.description, acs.productcode))
        FROM accountservices acs
        LEFT JOIN products p ON p.code = acs.productcode
        WHERE acs.accountid = a.id
          AND (:p_product IS NULL OR acs.productcode = :p_product)
          AND (:p_exclude_product IS NULL OR acs.productcode IS NULL OR acs.productcode <> :p_exclude_product)
      ) AS product,
      TO_CHAR((
        SELECT COUNT(*)
        FROM accountservices acs
        WHERE acs.accountid = a.id
      )) AS service_code,
      a.ownstatus AS service_type,
      (
        SELECT MIN(c.state)
        FROM acctcontacts c
        WHERE c.accountid = a.id
          AND (:p_region IS NULL OR UPPER(TRIM(c.state)) = UPPER(TRIM(:p_region)))
      ) AS region,
      (
        SELECT TRIM(NVL(c.firstname,'') || ' ' || NVL(c.lastname,''))
        FROM acctcontacts c
        WHERE c.accountid = a.id
          AND ROWNUM = 1
      ) AS customer
    FROM accounts a
    INNER JOIN (
      SELECT
        ast.accountid,
        ast.fromdate,
        ast.todate,
        NVL(lt.translated_string, NVL(sr.description, TO_CHAR(ast.reason))) AS reason,
        ROW_NUMBER() OVER (
          PARTITION BY ast.accountid
          ORDER BY ast.fromdate DESC NULLS LAST
        ) AS rn
      FROM acctstatus ast
      LEFT JOIN statreason sr ON sr.reasonnum = ast.reason
      LEFT JOIN lang_translations lt ON lt.numeric_id = TO_NUMBER(sr.descr) AND LOWER(lt.iso3code) = 'eng'
      WHERE ast.status = :p_status
        AND ast.fromdate >= :p_from
        AND ast.fromdate < :p_to_exclusive
    ) qpc ON qpc.accountid = a.id AND qpc.rn = 1
    WHERE a.providercode = :p_provider
      AND (
            :p_region IS NULL
            OR EXISTS (
                SELECT 1 FROM acctcontacts cx
                WHERE cx.accountid = a.id
                  AND UPPER(TRIM(cx.state)) = UPPER(TRIM(:p_region))
            )
          )
      AND (
            :p_product IS NULL
            OR EXISTS (
                SELECT 1 FROM accountservices sx
                WHERE sx.accountid = a.id AND sx.productcode = :p_product
            )
          )
AND (
      :p_exclude_product IS NULL
      OR NOT EXISTS (
          SELECT 1 FROM accountservices ep
          WHERE ep.accountid = a.id AND ep.productcode = :p_exclude_product
      )
)
      AND (
            :p_service_type IS NULL
            OR EXISTS (
                SELECT 1 FROM accountservices sx
                WHERE sx.accountid = a.id AND sx.servicetype = :p_service_type
            )
          )
      AND (
            :p_search IS NULL
            OR UPPER(a.code) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
            OR UPPER(a.accountname) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
            OR EXISTS (
                SELECT 1 FROM accountservices sx
                LEFT JOIN products px ON px.code = sx.productcode
                WHERE sx.accountid = a.id
                  AND (
                        UPPER(sx.servicecode) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
                        OR UPPER(sx.productcode) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
                        OR UPPER(px.description) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
                      )
            )
          )
    ORDER BY qpc.fromdate DESC, a.accountname
  ) inner_q
  WHERE ROWNUM <= :p_offset + :p_page_size
)
WHERE rnum > :p_offset");

    // Cancellation Events KPI — unique accounts that entered Permanently Closed / Completed Cancellation (T)
    // during the selected period. ACCTSTATUS.FROMDATE is the event date. Same FROMDATE-entry pattern as Suspended.
    public static readonly string CancellationAccountsBaseFrom = SuspendedAccountsBaseFrom;

    public static readonly string CountCancellationAccounts = @"
SELECT COUNT(DISTINCT a.id) AS CNT
" + CancellationAccountsBaseFrom;

    // One row per account that entered Permanently Closed (T) during the period (FROMDATE event).
    public static readonly string SelectCancellationAccounts = AccountExclusionQueryBuilder.Apply(
        @"
SELECT * FROM (
  SELECT inner_q.*, ROWNUM AS rnum FROM (
    SELECT
      a.id AS account_id,
      CAST(NULL AS NUMBER) AS service_id,
      a.code AS account_code,
      a.accountname AS account_name,
      :p_status AS status_code,
      qc.fromdate AS status_from_date,
      qc.todate AS status_end_date,
      qc.reason AS reason,
      (
        SELECT MIN(NVL(p.description, acs.productcode))
        FROM accountservices acs
        LEFT JOIN products p ON p.code = acs.productcode
        WHERE acs.accountid = a.id
          AND (:p_product IS NULL OR acs.productcode = :p_product)
          AND (:p_exclude_product IS NULL OR acs.productcode IS NULL OR acs.productcode <> :p_exclude_product)
      ) AS product,
      TO_CHAR((
        SELECT COUNT(*)
        FROM accountservices acs
        WHERE acs.accountid = a.id
      )) AS service_code,
      a.ownstatus AS service_type,
      (
        SELECT MIN(c.state)
        FROM acctcontacts c
        WHERE c.accountid = a.id
          AND (:p_region IS NULL OR UPPER(TRIM(c.state)) = UPPER(TRIM(:p_region)))
      ) AS region,
      (
        SELECT TRIM(NVL(c.firstname,'') || ' ' || NVL(c.lastname,''))
        FROM acctcontacts c
        WHERE c.accountid = a.id
          AND ROWNUM = 1
      ) AS customer
    FROM accounts a
    INNER JOIN (
      SELECT
        ast.accountid,
        ast.fromdate,
        ast.todate,
        NVL(lt.translated_string, NVL(sr.description, TO_CHAR(ast.reason))) AS reason,
        ROW_NUMBER() OVER (
          PARTITION BY ast.accountid
          ORDER BY ast.fromdate DESC NULLS LAST
        ) AS rn
      FROM acctstatus ast
      LEFT JOIN statreason sr ON sr.reasonnum = ast.reason
      LEFT JOIN lang_translations lt ON lt.numeric_id = TO_NUMBER(sr.descr) AND LOWER(lt.iso3code) = 'eng'
      WHERE ast.status = :p_status
        AND ast.fromdate >= :p_from
        AND ast.fromdate < :p_to_exclusive
    ) qc ON qc.accountid = a.id AND qc.rn = 1
    WHERE a.providercode = :p_provider
      AND (
            :p_region IS NULL
            OR EXISTS (
                SELECT 1 FROM acctcontacts cx
                WHERE cx.accountid = a.id
                  AND UPPER(TRIM(cx.state)) = UPPER(TRIM(:p_region))
            )
          )
      AND (
            :p_product IS NULL
            OR EXISTS (
                SELECT 1 FROM accountservices sx
                WHERE sx.accountid = a.id AND sx.productcode = :p_product
            )
          )
      AND (
            :p_exclude_product IS NULL
            OR NOT EXISTS (
                SELECT 1 FROM accountservices ep
                WHERE ep.accountid = a.id AND ep.productcode = :p_exclude_product
            )
          )
      AND (
            :p_service_type IS NULL
            OR EXISTS (
                SELECT 1 FROM accountservices sx
                WHERE sx.accountid = a.id AND sx.servicetype = :p_service_type
            )
          )
      AND (
            :p_search IS NULL
            OR UPPER(a.code) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
            OR UPPER(a.accountname) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
            OR EXISTS (
                SELECT 1 FROM accountservices sx
                LEFT JOIN products px ON px.code = sx.productcode
                WHERE sx.accountid = a.id
                  AND (
                        UPPER(sx.servicecode) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
                        OR UPPER(sx.productcode) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
                        OR UPPER(px.description) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
                      )
            )
          )
    ORDER BY qc.fromdate DESC, a.accountname
  ) inner_q
  WHERE ROWNUM <= :p_offset + :p_page_size
)
WHERE rnum > :p_offset");

    // Chart: unique accounts entering Permanently Closed (T) during each week bucket (FROMDATE event).
    public static readonly string CompletedCancellationsTrend = AccountExclusionQueryBuilder.Apply(
        @"
SELECT TRUNC(ast.fromdate, 'IW') AS period_start,
       COUNT(DISTINCT ast.accountid) AS value
FROM acctstatus ast
INNER JOIN accounts a ON a.id = ast.accountid
WHERE a.providercode = :p_provider
  AND ast.status = :p_status
  AND ast.fromdate >= :p_from
  AND ast.fromdate < :p_to_exclusive
  AND (
        :p_region IS NULL
        OR EXISTS (
            SELECT 1 FROM acctcontacts c
            WHERE c.accountid = a.id
              AND UPPER(TRIM(c.state)) = UPPER(TRIM(:p_region))
        )
      )
  AND (
        :p_product IS NULL
        OR EXISTS (
            SELECT 1 FROM accountservices acs
            WHERE acs.accountid = a.id AND acs.productcode = :p_product
        )
      )
  AND (
        :p_exclude_product IS NULL
        OR NOT EXISTS (
            SELECT 1 FROM accountservices ep
            WHERE ep.accountid = a.id AND ep.productcode = :p_exclude_product
        )
      )
  AND (
        :p_service_type IS NULL
        OR EXISTS (
            SELECT 1 FROM accountservices acs
            WHERE acs.accountid = a.id AND acs.servicetype = :p_service_type
        )
      )
GROUP BY TRUNC(ast.fromdate, 'IW')
ORDER BY period_start");

    // Chart: unique accounts entering Suspended (S) during each week bucket (FROMDATE event).
    // Same weekly FROMDATE query as other status-event charts; bind :p_status = S.
    public static readonly string SuspendedEventsTrend = CompletedCancellationsTrend;

    // Permanent Closed KPI — same T-status FROMDATE-entry definition as Cancellation Events.
    public static readonly string PermanentClosedAccountsBaseFrom = CancellationAccountsBaseFrom;
    public static readonly string CountPermanentClosedAccounts = CountCancellationAccounts;
    public static readonly string SelectPermanentClosedAccounts = SelectCancellationAccounts;
    public static readonly string PermanentClosedAccountsTrend = CompletedCancellationsTrend;

    // Chart: unique accounts entering Pending Cancellation (C) during each week bucket (FROMDATE event).
    public static readonly string NewPendingCancellationsTrend = AccountExclusionQueryBuilder.Apply(
        @"
SELECT TRUNC(ast.fromdate, 'IW') AS period_start,
       COUNT(DISTINCT ast.accountid) AS value
FROM acctstatus ast
INNER JOIN accounts a ON a.id = ast.accountid
WHERE a.providercode = :p_provider
  AND ast.status = :p_status
  AND ast.fromdate >= :p_from
  AND ast.fromdate < :p_to_exclusive
  AND (
        :p_region IS NULL
        OR EXISTS (
            SELECT 1 FROM acctcontacts c
            WHERE c.accountid = a.id
              AND UPPER(TRIM(c.state)) = UPPER(TRIM(:p_region))
        )
      )
  AND (
        :p_product IS NULL
        OR EXISTS (
            SELECT 1 FROM accountservices acs
            WHERE acs.accountid = a.id AND acs.productcode = :p_product
        )
      )
AND (
      :p_exclude_product IS NULL
      OR NOT EXISTS (
          SELECT 1 FROM accountservices ep
          WHERE ep.accountid = a.id AND ep.productcode = :p_exclude_product
      )
)
  AND (
        :p_service_type IS NULL
        OR EXISTS (
            SELECT 1 FROM accountservices acs
            WHERE acs.accountid = a.id AND acs.servicetype = :p_service_type
        )
      )
GROUP BY TRUNC(ast.fromdate, 'IW')
ORDER BY period_start");

    // One row per account that entered Suspended (S) during the period (FROMDATE event).
    public static readonly string SelectSuspendedAccounts = AccountExclusionQueryBuilder.Apply(
        @"
SELECT * FROM (
  SELECT inner_q.*, ROWNUM AS rnum FROM (
    SELECT
      account_id, service_id, account_code, account_name, status_code,
      status_from_date, status_end_date, reason, product, service_code,
      service_type, region, customer
    FROM (
      SELECT
        a.id AS account_id,
        acs.id AS service_id,
        a.code AS account_code,
        a.accountname AS account_name,
        ast.status AS status_code,
        ast.fromdate AS status_from_date,
        ast.todate AS status_end_date,
        NVL(lt.translated_string, NVL(sr.description, TO_CHAR(ast.reason))) AS reason,
        NVL(p.description, acs.productcode) AS product,
        acs.servicecode AS service_code,
        acs.servicetype AS service_type,
        c.state AS region,
        TRIM(NVL(c.firstname,'') || ' ' || NVL(c.lastname,'')) AS customer,
        ROW_NUMBER() OVER (
          PARTITION BY a.id
          ORDER BY ast.fromdate DESC NULLS LAST, acs.id NULLS LAST, c.email NULLS LAST) AS rn
      FROM accounts a
      INNER JOIN acctstatus ast
        ON ast.accountid = a.id
       AND ast.status = :p_status
       AND ast.fromdate >= :p_from
       AND ast.fromdate < :p_to_exclusive
      LEFT JOIN accountservices acs ON acs.accountid = a.id
      LEFT JOIN products p ON p.code = acs.productcode
      LEFT JOIN acctcontacts c ON c.accountid = a.id
      LEFT JOIN statreason sr ON sr.reasonnum = ast.reason
      LEFT JOIN lang_translations lt ON lt.numeric_id = TO_NUMBER(sr.descr) AND LOWER(lt.iso3code) = 'eng'
      WHERE a.providercode = :p_provider
        AND (
              :p_region IS NULL
              OR EXISTS (
                  SELECT 1 FROM acctcontacts cx
                  WHERE cx.accountid = a.id
                    AND UPPER(TRIM(cx.state)) = UPPER(TRIM(:p_region))
              )
            )
        AND (
              :p_product IS NULL
              OR EXISTS (
                  SELECT 1 FROM accountservices sx
                  WHERE sx.accountid = a.id AND sx.productcode = :p_product
              )
            )
AND (
      :p_exclude_product IS NULL
      OR NOT EXISTS (
          SELECT 1 FROM accountservices ep
          WHERE ep.accountid = a.id AND ep.productcode = :p_exclude_product
      )
)
        AND (
              :p_service_type IS NULL
              OR EXISTS (
                  SELECT 1 FROM accountservices sx
                  WHERE sx.accountid = a.id AND sx.servicetype = :p_service_type
              )
            )
        AND (
              :p_search IS NULL
              OR UPPER(a.code) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
              OR UPPER(a.accountname) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
              OR EXISTS (
                  SELECT 1 FROM accountservices sx
                  LEFT JOIN products px ON px.code = sx.productcode
                  WHERE sx.accountid = a.id
                    AND (
                          UPPER(sx.servicecode) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
                          OR UPPER(sx.productcode) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
                          OR UPPER(px.description) LIKE '%' || UPPER(TRIM(:p_search)) || '%'
                        )
              )
            )
    ) ranked
    WHERE ranked.rn = 1
    ORDER BY status_from_date DESC, account_name
  ) inner_q
  WHERE ROWNUM <= :p_offset + :p_page_size
)
WHERE rnum > :p_offset");

    public static readonly string StatusEventBaseFrom = AccountExclusionQueryBuilder.Apply(
        @"
FROM acctstatus ast
INNER JOIN accounts a ON a.id = ast.accountid
LEFT JOIN accountservices acs ON acs.accountid = a.id
LEFT JOIN products p ON p.code = acs.productcode
LEFT JOIN acctcontacts c ON c.accountid = a.id
LEFT JOIN statreason sr ON sr.reasonnum = ast.reason
LEFT JOIN lang_translations lt ON lt.numeric_id = TO_NUMBER(sr.descr) AND LOWER(lt.iso3code) = 'eng'
WHERE a.providercode = :p_provider
  AND ast.status = :p_status
  AND ast.fromdate >= :p_from
  AND ast.fromdate <= :p_to
  AND (:p_region IS NULL OR UPPER(TRIM(c.state)) = UPPER(:p_region))
  AND (:p_product IS NULL OR acs.productcode = :p_product)
  AND (:p_exclude_product IS NULL OR acs.productcode IS NULL OR acs.productcode <> :p_exclude_product)
  AND (:p_service_type IS NULL OR acs.servicetype = :p_service_type)
  AND (:p_search IS NULL
       OR UPPER(a.code) LIKE '%' || UPPER(:p_search) || '%'
       OR UPPER(a.accountname) LIKE '%' || UPPER(:p_search) || '%'
       OR UPPER(acs.productcode) LIKE '%' || UPPER(:p_search) || '%'
       OR UPPER(p.description) LIKE '%' || UPPER(:p_search) || '%')");

    public static readonly string CountStatusEvents = @"
SELECT COUNT(DISTINCT ast.accountid || '|' || TO_CHAR(ast.fromdate, 'YYYYMMDDHH24MISS') || '|' || NVL(TO_CHAR(acs.id), '0')) AS CNT
" + StatusEventBaseFrom;

    public static readonly string SelectStatusEvents = @"
SELECT * FROM (
  SELECT inner_q.*, ROWNUM AS rnum FROM (
    SELECT
      account_id, service_id, account_code, account_name, status_code,
      status_from_date, status_end_date, reason, product, service_code,
      service_type, region, customer
    FROM (
      SELECT
        a.id AS account_id,
        acs.id AS service_id,
        a.code AS account_code,
        a.accountname AS account_name,
        ast.status AS status_code,
        ast.fromdate AS status_from_date,
        ast.todate AS status_end_date,
        NVL(lt.translated_string, NVL(sr.description, TO_CHAR(ast.reason))) AS reason,
        NVL(p.description, acs.productcode) AS product,
        acs.servicecode AS service_code,
        acs.servicetype AS service_type,
        c.state AS region,
        TRIM(NVL(c.firstname,'') || ' ' || NVL(c.lastname,'')) AS customer,
        ROW_NUMBER() OVER (
          PARTITION BY a.id, ast.fromdate, NVL(acs.id, 0)
          ORDER BY c.email NULLS LAST) AS rn
      " + StatusEventBaseFrom + @"
    ) ranked
    WHERE ranked.rn = 1
    ORDER BY status_from_date DESC, account_name
  ) inner_q
  WHERE ROWNUM <= :p_offset + :p_page_size
)
WHERE rnum > :p_offset";

    public static readonly string StatusHistoryBaseFrom = AccountExclusionQueryBuilder.Apply(
        @"
FROM acctstatus ast
INNER JOIN accounts a ON a.id = ast.accountid
LEFT JOIN accountservices acs ON acs.accountid = a.id
LEFT JOIN products p ON p.code = acs.productcode
LEFT JOIN acctcontacts c ON c.accountid = a.id
LEFT JOIN statreason sr ON sr.reasonnum = ast.reason
LEFT JOIN lang_translations lt ON lt.numeric_id = TO_NUMBER(sr.descr) AND LOWER(lt.iso3code) = 'eng'
WHERE a.providercode = :p_provider
  AND ast.fromdate >= :p_from
  AND ast.fromdate <= :p_to
  AND (:p_status IS NULL OR ast.status = :p_status)
  AND (:p_region IS NULL OR UPPER(TRIM(c.state)) = UPPER(:p_region))
  AND (:p_product IS NULL OR acs.productcode = :p_product)
  AND (:p_exclude_product IS NULL OR acs.productcode IS NULL OR acs.productcode <> :p_exclude_product)
  AND (:p_service_type IS NULL OR acs.servicetype = :p_service_type)
  AND (:p_search IS NULL
       OR UPPER(a.code) LIKE '%' || UPPER(:p_search) || '%'
       OR UPPER(a.accountname) LIKE '%' || UPPER(:p_search) || '%'
       OR UPPER(acs.productcode) LIKE '%' || UPPER(:p_search) || '%'
       OR UPPER(p.description) LIKE '%' || UPPER(:p_search) || '%')");

    public static readonly string CountStatusHistory = @"
SELECT COUNT(DISTINCT ast.accountid || '|' || TO_CHAR(ast.fromdate, 'YYYYMMDDHH24MISS') || '|' || NVL(TO_CHAR(acs.id), '0')) AS CNT
" + StatusHistoryBaseFrom;

    public static readonly string SelectStatusHistory = @"
SELECT * FROM (
  SELECT inner_q.*, ROWNUM AS rnum FROM (
    SELECT
      account_id, service_id, account_code, account_name, status_code,
      status_from_date, status_end_date, reason, product, service_code,
      service_type, region, customer
    FROM (
      SELECT
        a.id AS account_id,
        acs.id AS service_id,
        a.code AS account_code,
        a.accountname AS account_name,
        ast.status AS status_code,
        ast.fromdate AS status_from_date,
        ast.todate AS status_end_date,
        NVL(lt.translated_string, NVL(sr.description, TO_CHAR(ast.reason))) AS reason,
        NVL(p.description, acs.productcode) AS product,
        acs.servicecode AS service_code,
        acs.servicetype AS service_type,
        c.state AS region,
        TRIM(NVL(c.firstname,'') || ' ' || NVL(c.lastname,'')) AS customer,
        ROW_NUMBER() OVER (
          PARTITION BY a.id, ast.fromdate, NVL(acs.id, 0)
          ORDER BY c.email NULLS LAST) AS rn
      " + StatusHistoryBaseFrom + @"
    ) ranked
    WHERE ranked.rn = 1
    ORDER BY status_from_date DESC
  ) inner_q
  WHERE ROWNUM <= :p_offset + :p_page_size
)
WHERE rnum > :p_offset";

    public static readonly string ActivationTrend = AccountExclusionQueryBuilder.Apply(
        @"
SELECT TRUNC(a.registrationdate, 'IW') AS period_start,
       COUNT(DISTINCT a.id) AS value
FROM accounts a
WHERE a.providercode = :p_provider
  AND a.registrationdate >= :p_from
  AND a.registrationdate < :p_to_exclusive
  AND (
        :p_region IS NULL
        OR EXISTS (
            SELECT 1 FROM acctcontacts c
            WHERE c.accountid = a.id
              AND UPPER(TRIM(c.state)) = UPPER(TRIM(:p_region))
        )
      )
  AND (
        :p_product IS NULL
        OR EXISTS (
            SELECT 1 FROM accountservices acs
            WHERE acs.accountid = a.id AND acs.productcode = :p_product
        )
      )
AND (
      :p_exclude_product IS NULL
      OR NOT EXISTS (
          SELECT 1 FROM accountservices ep
          WHERE ep.accountid = a.id AND ep.productcode = :p_exclude_product
      )
)
  AND (
        :p_service_type IS NULL
        OR EXISTS (
            SELECT 1 FROM accountservices acs
            WHERE acs.accountid = a.id AND acs.servicetype = :p_service_type
        )
      )
GROUP BY TRUNC(a.registrationdate, 'IW')
ORDER BY period_start");

    public static readonly string CancellationTrend = AccountExclusionQueryBuilder.Apply(
        @"
SELECT TRUNC(ast.fromdate, 'IW') AS period_start,
       COUNT(DISTINCT ast.accountid || '|' || TO_CHAR(ast.fromdate, 'YYYYMMDDHH24MISS')) AS value
FROM acctstatus ast
INNER JOIN accounts a ON a.id = ast.accountid
LEFT JOIN accountservices acs ON acs.accountid = a.id
LEFT JOIN acctcontacts c ON c.accountid = a.id
WHERE a.providercode = :p_provider
  AND ast.status = :p_status
  AND ast.fromdate >= :p_from
  AND ast.fromdate <= :p_to
  AND (:p_region IS NULL OR UPPER(TRIM(c.state)) = UPPER(:p_region))
  AND (:p_product IS NULL OR acs.productcode = :p_product)
  AND (:p_exclude_product IS NULL OR acs.productcode IS NULL OR acs.productcode <> :p_exclude_product)
  AND (:p_service_type IS NULL OR acs.servicetype = :p_service_type)
GROUP BY TRUNC(ast.fromdate, 'IW')
ORDER BY period_start");

    public static readonly string StatusDistribution = AccountExclusionQueryBuilder.Apply(
        @"
SELECT ast.status AS label,
       COUNT(DISTINCT ast.accountid || '|' || TO_CHAR(ast.fromdate, 'YYYYMMDDHH24MISS')) AS value
FROM acctstatus ast
INNER JOIN accounts a ON a.id = ast.accountid
WHERE a.providercode = :p_provider
  AND ast.fromdate >= :p_from
  AND ast.fromdate <= :p_to
  AND (
        :p_exclude_product IS NULL
        OR NOT EXISTS (
            SELECT 1 FROM accountservices ep
            WHERE ep.accountid = a.id AND ep.productcode = :p_exclude_product
        )
      )
GROUP BY ast.status
ORDER BY value DESC");

    public static readonly string RegionAnalysis = AccountExclusionQueryBuilder.Apply(
        @"
SELECT NVL(region, 'Unknown') AS label,
       COUNT(DISTINCT account_id) AS value
FROM (
  SELECT a.id AS account_id,
         c.state AS region,
         ROW_NUMBER() OVER (PARTITION BY a.id ORDER BY c.email NULLS LAST) AS rn
  FROM accounts a
  LEFT JOIN acctcontacts c ON c.accountid = a.id
  WHERE a.providercode = :p_provider
    AND a.registrationdate >= :p_from
    AND a.registrationdate < :p_to_exclusive
    AND (
          :p_exclude_product IS NULL
          OR NOT EXISTS (
              SELECT 1 FROM accountservices ep
              WHERE ep.accountid = a.id AND ep.productcode = :p_exclude_product
          )
        )
) ranked
WHERE ranked.rn = 1
GROUP BY NVL(region, 'Unknown')
ORDER BY value DESC");

    public static readonly string ProductAnalysis = AccountExclusionQueryBuilder.Apply(
        @"
SELECT NVL(acs.productcode, 'Unknown') AS label,
       COUNT(DISTINCT a.id) AS value
FROM accounts a
LEFT JOIN accountservices acs ON acs.accountid = a.id
WHERE a.providercode = :p_provider
  AND a.registrationdate >= :p_from
  AND a.registrationdate < :p_to_exclusive
  AND (:p_product IS NULL OR acs.productcode = :p_product)
  AND (:p_exclude_product IS NULL OR acs.productcode IS NULL OR acs.productcode <> :p_exclude_product)
GROUP BY NVL(acs.productcode, 'Unknown')
ORDER BY value DESC");

    public static readonly string CancellationByReason = AccountExclusionQueryBuilder.Apply(
        @"
SELECT NVL(lt.translated_string, NVL(sr.description, NVL(TO_CHAR(ast.reason), 'Unknown'))) AS label,
       COUNT(DISTINCT ast.accountid || '|' || TO_CHAR(ast.fromdate, 'YYYYMMDDHH24MISS')) AS value
FROM acctstatus ast
INNER JOIN accounts a ON a.id = ast.accountid
LEFT JOIN statreason sr ON sr.reasonnum = ast.reason
LEFT JOIN lang_translations lt ON lt.numeric_id = TO_NUMBER(sr.descr) AND LOWER(lt.iso3code) = 'eng'
WHERE a.providercode = :p_provider
  AND ast.status = :p_status
  AND ast.fromdate >= :p_from
  AND ast.fromdate <= :p_to
  AND (
        :p_exclude_product IS NULL
        OR NOT EXISTS (
            SELECT 1 FROM accountservices ep
            WHERE ep.accountid = a.id AND ep.productcode = :p_exclude_product
        )
      )
GROUP BY NVL(lt.translated_string, NVL(sr.description, NVL(TO_CHAR(ast.reason), 'Unknown')))
ORDER BY value DESC");

    public const string FilterRegions = @"
SELECT DISTINCT c.state AS value
FROM acctcontacts c
INNER JOIN accounts a ON a.id = c.accountid
WHERE a.providercode = :p_provider AND c.state IS NOT NULL
ORDER BY c.state";

    public static readonly string FilterProducts = $@"
SELECT DISTINCT acs.productcode AS value
FROM accountservices acs
INNER JOIN accounts a ON a.id = acs.accountid
WHERE a.providercode = :p_provider AND acs.productcode IS NOT NULL
  AND acs.productcode <> '{ExcludedProductCode}'
ORDER BY acs.productcode";

    public const string FilterServiceTypes = @"
SELECT DISTINCT acs.servicetype AS value
FROM accountservices acs
INNER JOIN accounts a ON a.id = acs.accountid
WHERE a.providercode = :p_provider AND acs.servicetype IS NOT NULL
ORDER BY acs.servicetype";
}
