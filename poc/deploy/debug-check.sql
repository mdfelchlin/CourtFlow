SELECT o.id AS org_id, o.slug FROM organizations o;
SELECT u.id, u.email FROM "AspNetUsers" u;
SELECT m.organization_id, m.user_id, m.role FROM organization_memberships m;
SELECT id, name, organization_id FROM tournaments;
SELECT r.name AS role, ur.user_id FROM "AspNetRoles" r JOIN "AspNetUserRoles" ur ON ur.role_id = r.id;
