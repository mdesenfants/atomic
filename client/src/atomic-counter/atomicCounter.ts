export interface ITenant {
    tenantName: string;
    origins: string[];
    writeKeys: string[];
    readKeys: string[];
}

export async function increment(tenant: string, app: string, counter: string, key: string): Promise<void> {
    await fetch(`https://atomiccounter.azurewebsites.net/api/tenant/${tenant}/app/${app}/counter/${counter}/increment?key=${key}`, {
        headers: {
            "Accept": "application/json",
            "Content-Type": "application/json"
        },
        method: "POST"
    });
}

export async function count(tenant: string, app: string, counter: string, key: string): Promise<number> {
    return await fetch(`https://atomiccounter.azurewebsites.net/api/tenant/${tenant}/app/${app}/counter/${counter}/count?key=${key}`, {
        headers: {
            "Accept": "application/json",
            "Content-Type": "application/json"
        },
        method: "GET"
    }).then(v => v.json() as unknown as number);
}

export async function getAuthToken(token: string): Promise<string> {
    const response = await fetch("https://atomiccounter.azurewebsites.net/.auth/login/google", {
        body: JSON.stringify({
            id_token: token
        }),
        headers: {
            "Content-Type": "application/json"
        },
        method: "POST",
    });

    const ez = await response.json();

    return ez.authenticationToken as string;
}

export class AtomicCounterClient {
    private token: string;

    private tenants: ITenant[];

    constructor(authToken: string) {
        this.token = authToken;
    }

    public async createTenant(tenant: string) {
        return await fetch("https://atomiccounter.azurewebsites.net/api/tenant/" + encodeURI(tenant), {
            headers: {
                "Accept": "application/json",
                "Content-Type": "application/json",
                "X-ZUMO-AUTH": this.token
            },
            method: "POST"
        }).then(t => t.json() as unknown as ITenant);
    }

    public async getTenant(tenant: string) {
        return await fetch("https://atomiccounter.azurewebsites.net/api/tenant/" + encodeURI(tenant), {
            headers: {
                "Accept": "application/json",
                "Content-Type": "application/json",
                "X-ZUMO-AUTH": this.token
            },
            method: "GET",
        }).then(t => t.json() as unknown as ITenant);
    }

    public async increment(tenant: string, app: string, counter: string): Promise<void> {
        if (!this.tenants) {
            this.tenants = [await this.getTenant(tenant)];
        }

        const key = this.tenants.filter(t => t.tenantName === tenant)[0].writeKeys[0];

        await increment(tenant, app, counter, key);
    }

    public async count(tenant: string, app: string, counter: string): Promise<number> {
        if (!this.tenants) {
            this.tenants = [await this.getTenant(tenant)];
        }

        const key = this.tenants.filter(t => t.tenantName === tenant)[0].readKeys[0];

        return await count(tenant, app, counter, key).catch(() => 0);
    }

    public async reset(tenant: string, app: string, counter: string): Promise<void> {
        await fetch(`https://atomiccounter.azurewebsites.net/api/tenant/${tenant}/app/${app}/counter/${counter}`, {
            headers: {
                "Accept": "application/json",
                "Content-Type": "application/json",
                "X-ZUMO-AUTH": this.token
            },
            method: "DELETE",
        });
    }
}