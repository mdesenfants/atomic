export interface ITenant {
    tenantName: string;
    origins: string[];
    writeKeys: string[];
    readKeys: string[];
}

export async function increment(key: string): Promise<void> {
    await fetch("https://atomiccounter.azurewebsites.net/api/tenant/bill/app/bill/counter/bill/increment?key=" + key, {
        headers: {
            "Accept": "application/json",
            "Content-Type": "application/json"
        },
        method: "POST"
    });
}

export async function count(key: string): Promise<number> {
    return await fetch("https://atomiccounter.azurewebsites.net/api/tenant/bill/app/bill/counter/bill/count?key=" + key, {
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

    public async createTenant() {
        return await fetch("https://atomiccounter.azurewebsites.net/api/tenant/bill", {
            headers: {
                "Accept": "application/json",
                "Content-Type": "application/json",
                "X-ZUMO-AUTH": this.token
            },
            method: "POST"
        }).then(t => t.json() as unknown as ITenant);
    }

    public async getTenant() {
        return await fetch("https://atomiccounter.azurewebsites.net/api/tenant/bill", {
            headers: {
                "Accept": "application/json",
                "Content-Type": "application/json",
                "X-ZUMO-AUTH": this.token
            },
            method: "GET",
        }).then(t => t.json() as unknown as ITenant);
    }

    public async increment(): Promise<void> {
        if (!this.tenants) {
            this.tenants = [await this.getTenant()];
        }

        await increment(this.tenants[0].writeKeys[0]);
    }

    public async count(): Promise<number> {
        if (!this.tenants) {
            this.tenants = [await this.getTenant()];
        }

        return await count(this.tenants[0].readKeys[0]);
    }
}